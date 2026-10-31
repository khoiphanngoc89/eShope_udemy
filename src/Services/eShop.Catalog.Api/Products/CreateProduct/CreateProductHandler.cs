using eShop.Catalog.Api.Data;
using FluentValidation;

namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, string Description, string ImageFile, List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResult(Guid Id);
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Product price must be greater than zero.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Product description is required.");
        RuleFor(x => x.ImageFile).NotEmpty().WithMessage("Product image file is required.");
        RuleFor(x => x.Categories).NotEmpty().WithMessage("At least one category is required.");
    }
}

public sealed class CreateProductHandler(ICatalogUnitOfWork unitOfWork) : ICommandHandler<CreateProductCommand, CreateProductResult>
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
            Categories = command.Categories,
            IsActive = true,
            IsDeleted = false
        };

        unitOfWork.Products.Create(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(product.Id);
    }
}