using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using eShop.Catalog.Api.Abstractions;
using eShop.Catalog.Api.Products.ImportProducts;

namespace eShop.Catalog.Api.Services;

public sealed class ImportProductsReader : IImportProductsReader
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public IReadOnlyList<ImportedProduct> Read(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var worksheetPath = FindWorksheetPath(archive, "CHI PHÍ");
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidDataException("The workbook does not contain a worksheet.");
        using var worksheetStream = worksheetEntry.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var rows = worksheet
            .Descendants(SpreadsheetNamespace + "row")
            .Select(row => new WorksheetRow((int?)row.Attribute("r") ?? 0, ReadRow(row, sharedStrings)))
            .Where(row => row.Cells.Count > 0)
            .ToList();

        var headerRowIndex = rows.FindIndex(row =>
            row.Cells.Values.Any(value => NormalizeHeader(value) == NormalizeHeader("Tên sản phẩm")));
        if (headerRowIndex < 0)
        {
            throw new InvalidDataException("The 'CHI PHÍ' worksheet must contain a 'Tên sản phẩm' header.");
        }

        var headers = rows[headerRowIndex].Cells.ToDictionary(
            cell => NormalizeHeader(cell.Value),
            cell => cell.Key,
            StringComparer.OrdinalIgnoreCase);
        var columns = GetColumns(headers);
        var products = new List<ImportedProduct>(rows.Count - headerRowIndex - 1);

        for (var index = headerRowIndex + 1; index < rows.Count; index++)
        {
            var worksheetRow = rows[index];
            var row = worksheetRow.Cells;
            var name = CellValue(row, columns.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var rowNumber = worksheetRow.RowNumber;
            var sewingCost = OptionalDecimal(row, columns.SewingCost, "SewingCost", rowNumber);
            var fabricCost = OptionalDecimal(row, columns.FabricCostAccessoriesCost, "FabricCostAccessoriesCost", rowNumber);
            var packagingCost = OptionalDecimal(row, columns.PackagingCost, "PackagingCost", rowNumber);
            var costPrice = OptionalDecimal(row, columns.CostPrice, "CostPrice", rowNumber);
            if (costPrice == 0)
            {
                costPrice = sewingCost + fabricCost + packagingCost;
            }

            products.Add(new ImportedProduct(
                name,
                OptionalInteger(row, columns.Amount, "Amount", rowNumber),
                costPrice,
                sewingCost,
                fabricCost,
                packagingCost,
                OptionalValue(row, columns.Description),
                OptionalValue(row, columns.ImageFile),
                OptionalValue(row, columns.Categories)
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()));
        }

        if (products.Count == 0)
        {
            throw new InvalidDataException("The workbook does not contain any product rows.");
        }

        return products;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        return XDocument.Load(stream)
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string FindWorksheetPath(ZipArchive archive, string worksheetName)
    {
        using var workbookStream = (archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("The file is not a valid Excel workbook."))
            .Open();
        var sheet = XDocument.Load(workbookStream)
            .Descendants(SpreadsheetNamespace + "sheet")
            .FirstOrDefault(item => string.Equals((string?)item.Attribute("name"), worksheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"The workbook does not contain a '{worksheetName}' worksheet.");
        var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id")
            ?? throw new InvalidDataException("The workbook worksheet relationship is missing.");

        using var relationshipsStream = (archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("The workbook worksheet relationships are missing."))
            .Open();
        var relationship = XDocument.Load(relationshipsStream)
            .Root?
            .Elements(PackageRelationshipNamespace + "Relationship")
            .FirstOrDefault(item => (string?)item.Attribute("Id") == relationshipId);
        var target = (string?)relationship?.Attribute("Target")
            ?? throw new InvalidDataException($"The '{worksheetName}' worksheet could not be resolved.");

        var normalizedTarget = target.Replace('\\', '/');
        if (normalizedTarget.StartsWith("/", StringComparison.Ordinal))
        {
            return normalizedTarget.TrimStart('/');
        }

        var path = $"xl/{normalizedTarget}";
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidDataException("The worksheet path is invalid.");
                }

                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private static Dictionary<int, string> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var cells = new Dictionary<int, string>();
        foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            var reference = (string?)cell.Attribute("r")
                ?? throw new InvalidDataException("A worksheet cell is missing its reference.");
            var column = GetColumnIndex(reference);
            var type = (string?)cell.Attribute("t");
            string value;
            if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
            }
            else
            {
                var rawValue = (string?)cell.Element(SpreadsheetNamespace + "v") ?? string.Empty;
                if (type == "s" && int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    if (index < 0 || index >= sharedStrings.Count)
                    {
                        throw new InvalidDataException($"Shared string index '{index}' is out of range.");
                    }

                    value = sharedStrings[index];
                }
                else
                {
                    value = rawValue;
                }
            }

            cells[column] = value;
        }

        return cells;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var column = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }

            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }

        if (column == 0)
        {
            throw new InvalidDataException($"Invalid worksheet cell reference '{cellReference}'.");
        }

        return column - 1;
    }

    private static ImportColumns GetColumns(IReadOnlyDictionary<string, int> headers)
    {
        int Required(params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValue(NormalizeHeader(name), out var column))
                {
                    return column;
                }
            }

            throw new InvalidDataException($"Required column '{names[0]}' is missing from the 'CHI PHÍ' header row.");
        }

        int? Optional(params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValue(NormalizeHeader(name), out var column))
                {
                    return column;
                }
            }

            return null;
        }

        return new ImportColumns(
            Required("Name", "Product Name", "Tên sản phẩm"),
            Optional("Amount", "Quantity", "Số lượng", "Số lượng đặt may"),
            Required("CostPrice", "Cost Price", "Giá vốn/SP"),
            Required("SewingCost", "Sewing Cost", "Chi phí may/SP"),
            Required("FabricCostAccessoriesCost", "Fabric Cost Accessories Cost", "Fabric and Accessories Cost", "Vải + Phụ liệu/SP"),
            Required("PackagingCost", "Packaging Cost", "Đóng gói/SP"),
            Optional("Description", "Mô tả"),
            Optional("ImageFile", "Image File", "Hình ảnh"),
            Optional("Categories", "Category", "Danh mục"));
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetterOrDigit))
            .ToLowerInvariant();
    }

    private static string CellValue(IReadOnlyDictionary<int, string> row, int column) =>
        row.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

    private static string OptionalValue(IReadOnlyDictionary<int, string> row, int? column) =>
        column is null ? string.Empty : CellValue(row, column.Value);

    private static decimal OptionalDecimal(IReadOnlyDictionary<int, string> row, int column, string field, int rowNumber)
    {
        var value = CellValue(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Row {rowNumber}: '{field}' must be a valid number.");
    }

    private static int OptionalInteger(IReadOnlyDictionary<int, string> row, int? column, string field, int rowNumber)
    {
        if (column is null || string.IsNullOrWhiteSpace(CellValue(row, column.Value)))
        {
            return 0;
        }

        var value = CellValue(row, column.Value);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Row {rowNumber}: '{field}' must be a whole number.");
    }

    private sealed record WorksheetRow(int RowNumber, Dictionary<int, string> Cells);

    private sealed record ImportColumns(
        int Name,
        int? Amount,
        int CostPrice,
        int SewingCost,
        int FabricCostAccessoriesCost,
        int PackagingCost,
        int? Description,
        int? ImageFile,
        int? Categories);
}