using eShop.Catalog.Api.Data;
using FluentValidation;

namespace eShop.Catalog.Api.Products.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    int Amount,
    decimal CostPrice,
    decimal SewingCost,
    decimal FabricCostAccessoriesCost,
    decimal PackagingCost,
    string Description,
    string ImageFile,
    List<string> Categories) : ICommand<CreateProductResult>;
public sealed record CreateProductResult(Guid Id);
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Product amount must be non-negative.");
        RuleFor(x => x.CostPrice).GreaterThan(0).WithMessage("Product cost price must be greater than zero.");
        RuleFor(x => x.SewingCost).GreaterThanOrEqualTo(0).WithMessage("Sewing cost must be a non-negative value.");
        RuleFor(x => x.FabricCostAccessoriesCost).GreaterThanOrEqualTo(0).WithMessage("Fabric and accessories cost must be a non-negative value.");
        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0).WithMessage("Packaging cost must be a non-negative value.");
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
            Amount = command.Amount,
            CostPrice = command.CostPrice,
            SewingCost = command.SewingCost,
            FabricCostAccessoriesCost = command.FabricCostAccessoriesCost,
            PackagingCost = command.PackagingCost,
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