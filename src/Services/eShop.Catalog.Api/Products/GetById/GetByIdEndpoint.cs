using BuildingBlock.Extensions;

namespace eShop.Catalog.Api.Products.GetById;

public sealed record GetByIdRequest(Guid Id);
public sealed record GetByIdResponse(Product Product);
public sealed class GetByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetByIdQuery(id), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetById")
        .WithSummary("Get a product by ID")
        .WithDescription("Retrieves a product by its ID.")
        .WithTags("Products")
        .WithException()
        .Produces<GetByIdResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}