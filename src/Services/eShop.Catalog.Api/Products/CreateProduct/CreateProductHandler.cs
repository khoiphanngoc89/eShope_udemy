namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, string Description, string ImageFile, List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResult(Guid Id);

internal sealed class CreateProductHandler(IDocumentSession session) : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Price = command.Price,
            Description = command.Description,
            ImageFile = command.ImageFile,
            Categories = command.Categories
        };

        // Here you would typically save the product to a database or perform other business logic.
        session.Store(product);
        await session.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(product.Id);
    }
}