using BuildingBlock.Extensions;
using BuildingBlock.Models;

namespace eShop.Catalog.Api.Products.GetProducts;

public sealed record GetProductsRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? Name = null,
    decimal? CostPrice = null,
    decimal? SewingCost = null,
    decimal? FabricCostAccessoriesCost = null,
    decimal? PackagingCost = null,
    string? Category = null,
    ProductOrderBy OrderBy = ProductOrderBy.CreatedAt,
    OrderDirection OrderDirection = OrderDirection.Desc);

public sealed record GetProductsResponse(PaginatedResult<Product> Products);
public sealed class GetProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (
            ISender sender,
            [AsParameters] GetProductsRequest request,
            CancellationToken cancellationToken = default) =>
        {
            var query = new GetProductsQuery(
                request.PageNumber,
                request.PageSize,
                request.Name,
                request.CostPrice,
                request.SewingCost,
                request.FabricCostAccessoriesCost,
                request.PackagingCost,
                request.Category,
                request.OrderBy,
                request.OrderDirection);
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetProducts")
        .WithSummary("Get a page of products")
        .WithDescription("Returns a page of catalog products with optional filters and sorting. Defaults to CreatedAt descending.")
        .WithTags("Products")
        .WithException()
        .Produces<PaginatedResult<Product>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }
}
