using BuildingBlock.Extensions;
using BuildingBlock.Models;

namespace eShop.Catalog.Api.Products.GetProducts;

public sealed class GetProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (
            ISender sender,
            int pageNumber = 1,
            int pageSize = 10,
            string? name = null,
            decimal? price = null,
            string? category = null,
            CancellationToken cancellationToken = default) =>
        {
            var request = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
            var result = await sender.Send(
                new GetProductsQuery(request.PageNumber, request.PageSize, name, price, category),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetProducts")
        .WithSummary("Get a page of products")
        .WithDescription("Returns one page of catalog products. Optional name, price, and category search.")
        .WithTags("Products")
        .WithException()
        .Produces<PaginatedResult<Product>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
