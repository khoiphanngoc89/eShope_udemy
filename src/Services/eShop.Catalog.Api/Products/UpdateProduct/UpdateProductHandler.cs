using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Exceptions;
using FluentValidation;
using Mapster;

namespace eShop.Catalog.Api.Products.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    int Amount,
    decimal CostPrice,
    decimal SewingCost,
    decimal FabricCostAccessoriesCost,
    decimal PackagingCost,
    string Description,
    string ImageFile,
    List<string> Categories) : ICommand<UpdateProductResult>;
public sealed record UpdateProductResult(bool IsSuccess);
public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product ID is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("Product amount must be non-negative.");
        RuleFor(x => x.CostPrice).GreaterThan(0).WithMessage("Product cost price must be greater than zero.");
        RuleFor(x => x.SewingCost).GreaterThanOrEqualTo(0).WithMessage("Sewing cost must be non-negative.");
        RuleFor(x => x.FabricCostAccessoriesCost).GreaterThanOrEqualTo(0).WithMessage("Fabric and accessories cost must be non-negative.");
        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0).WithMessage("Packaging cost must be non-negative.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Product description is required.");
        RuleFor(x => x.ImageFile).NotEmpty().WithMessage("Product image file is required.");
        RuleFor(x => x.Categories).NotEmpty().WithMessage("At least one category is required.");
    }
}
public sealed class UpdateProductHandler(ICatalogUnitOfWork unitOfWork) : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var products = unitOfWork.Products;
        var product = await products.GetByIdAsync(command.Id, cancellationToken);
        if (product is null)
        {
            throw new ProductNotFoundException(command.Id);
        }

        command.Adapt(product);
        products.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateProductResult(true);
    }
}