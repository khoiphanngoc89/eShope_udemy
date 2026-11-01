
namespace eShop.Catalog.Api.Products.ImportProducts;

public sealed record ImportProductsResponse(int ImportedCount);
public sealed class ImportProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products/import", async (IFormFile? file, IImportProductsReader reader, ISender sender, CancellationToken cancellationToken) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            if (!Path.GetExtension(file.FileName)
                .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Only .xlsx files are supported.");
            }

            await using var stream = file.OpenReadStream();
            IReadOnlyList<ImportedProduct> rows;
            try
            {
                rows = reader.Read(stream);
            }
            catch (InvalidDataException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }

            var result = await sender.Send(new ImportProductsCommand(rows), cancellationToken);
            return Results.Ok(new ImportProductsResponse(result.ImportedCount));
        })
        .WithName("ImportProducts")
        .WithSummary("Import products from an Excel workbook")
        .WithDescription("Uploads an .xlsx workbook with product columns and imports its rows.")
        .WithTags("Products")
        .WithException()
        .DisableAntiforgery()
        .Produces<ImportProductsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}