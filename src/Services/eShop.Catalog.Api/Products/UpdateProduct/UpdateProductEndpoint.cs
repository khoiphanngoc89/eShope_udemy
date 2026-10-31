using BuildingBlock.Extensions;
using eShop.Catalog.Api.Exceptions;

namespace eShop.Catalog.Api.Products.UpdateProduct;

public sealed record UpdateProductRequest(Guid Id, string Name, decimal Price, string Description, string ImageFile, List<string> Categories);
public sealed record UpdateProductResponse(bool IsSuccess);
public sealed class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            if (id != request.Id || request.Id == Guid.Empty)
            {
                return Results.NotFound("The product ID in the URL does not match the product ID in the request body.");
            }
            var command = request.Adapt<UpdateProductCommand>();
            var resp = await sender.Send(command, cancellationToken);
            var response = new UpdateProductResponse(resp.IsSuccess);
            return Results.Ok(response);
        })
        .WithName("UpdateProduct")
        .WithSummary("Update a product")
        .WithDescription("Updates a product and returns the updated product result.")
        .WithException()
        .Produces<UpdateProductResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}