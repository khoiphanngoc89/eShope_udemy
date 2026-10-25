namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, string Description, string ImageFile, List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResult(Guid Id);

internal sealed class CreateProductHandler : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            Description = request.Description,
            ImageFile = request.ImageFile,
            Categories = request.Categories
        };

        // Here you would typically save the product to a database or perform other business logic.

        return Task.FromResult(new CreateProductResult(product.Id));
    }
}