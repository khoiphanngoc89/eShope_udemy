using Carter;

namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductRequest(string Name, decimal Price, string Description, string ImageFile, List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResponse(Guid Id);
public sealed class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (CreateProductCommand command, ICommandHandler<CreateProductCommand, CreateProductResult> handler) =>
        {
            var result = await handler.Handle(command, CancellationToken.None);
            return Results.Created($"/products/{result.Id}", result);
        })
        .WithName("CreateProduct")
        .WithSummary("Create a product")
        .WithDescription("Creates a product and returns the created product result.")
        .Produces<CreateProductResult>(StatusCodes.Status201Created);
    }


}