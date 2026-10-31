namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductRequest(string Name, decimal Price, string Description, string ImageFile, List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResponse(Guid Id);
public sealed class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/products", async (CreateProductCommand command, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);
            return Results.Created($"/products/{result.Id}", result);
        })
        .WithName("CreateProduct")
        .WithSummary("Create a product")
        .WithDescription("Creates a product and returns the created product result.")
        .WithTags("Products")
        .Produces<CreateProductResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);
    }


}