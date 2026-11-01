using System.IO.Compression;
using System.Text;
using eShop.Catalog.Api.Services;

namespace eShop.Catalog.Api.Tests;

public class ImportProductsReaderTests
{
    [Fact]
    public void Read_WhenWorkbookHasVietnameseCostSheet_MapsProductColumns()
    {
        using var workbook = CreateWorkbook();
        var reader = new ImportProductsReader();

        var products = reader.Read(workbook);

        var product = Assert.Single(products);
        Assert.Equal("Đầm Lily size 3 (Trắng phối yếm hồng)", product.Name);
        Assert.Equal(10, product.Amount);
        Assert.Equal(205000m, product.CostPrice);
        Assert.Equal(175000m, product.SewingCost);
        Assert.Equal(0m, product.FabricCostAccessoriesCost);
        Assert.Equal(30000m, product.PackagingCost);
        Assert.Empty(product.Categories);
    }

    [Fact]
    public void Read_WhenPerProductCostIsBlank_DerivesItFromCostComponents()
    {
        using var workbook = CreateWorkbook(costPrice: null);
        var reader = new ImportProductsReader();

        var product = Assert.Single(reader.Read(workbook));

        Assert.Equal(205000m, product.CostPrice);
    }

    [Fact]
    public void Read_WhenCostAndComponentCellsAreBlank_UsesZeroCosts()
    {
        using var workbook = CreateWorkbook(costPrice: null, sewingCost: null, packagingCost: null);
        var reader = new ImportProductsReader();

        var product = Assert.Single(reader.Read(workbook));

        Assert.Equal(0m, product.CostPrice);
        Assert.Equal(0m, product.SewingCost);
        Assert.Equal(0m, product.PackagingCost);
    }

    private static MemoryStream CreateWorkbook(
        int? costPrice = 205000,
        int? sewingCost = 175000,
        int? packagingCost = 30000)
    {
        var sharedStrings = new[]
        {
            "STT",
            "Tên sản phẩm",
            "Số lượng đặt may",
            "Chi phí may/SP",
            "Vải + Phụ liệu/SP",
            "Đóng gói/SP",
            "Giá vốn/SP",
            "Tổng giá vốn",
            "Đầm Lily size 3 (Trắng phối yếm hồng)"
        };
        var sharedStringXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{sharedStrings.Length}" uniqueCount="{sharedStrings.Length}">
              {string.Concat(sharedStrings.Select(value => $"<si><t>{System.Security.SecurityElement.Escape(value)}</t></si>"))}
            </sst>
            """;
        var costCell = costPrice is null ? string.Empty : $"<c r=\"G2\"><v>{costPrice}</v></c>";
        var worksheetXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1">
                  {SharedStringCell("A1", 0)}
                  {SharedStringCell("B1", 1)}
                  {SharedStringCell("C1", 2)}
                  {SharedStringCell("D1", 3)}
                  {SharedStringCell("E1", 4)}
                  {SharedStringCell("F1", 5)}
                  {SharedStringCell("G1", 6)}
                  {SharedStringCell("H1", 7)}
                </row>
                <row r="2">
                  <c r="A2"><v>1</v></c>
                  {SharedStringCell("B2", 8)}
                  <c r="C2"><v>10</v></c>
                  {(sewingCost is null ? string.Empty : $"<c r=\"D2\"><v>{sewingCost}</v></c>")}
                  {(packagingCost is null ? string.Empty : $"<c r=\"F2\"><v>{packagingCost}</v></c>")}
                  {costCell}
                  <c r="H2"><f>C2*G2</f><v>2050000</v></c>
                </row>
              </sheetData>
            </worksheet>
            """;

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="CHI PHÍ" sheetId="1" r:id="rId1" /></sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
                </Relationships>
                """);
            WriteEntry(archive, "xl/sharedStrings.xml", sharedStringXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", worksheetXml);
        }

        stream.Position = 0;
        return stream;
    }

    private static string SharedStringCell(string reference, int index) =>
        $"<c r=\"{reference}\" t=\"s\"><v>{index}</v></c>";

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
