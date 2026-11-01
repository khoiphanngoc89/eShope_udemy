using eShop.Catalog.Api.Data;
using FluentValidation;

namespace eShop.Catalog.Api.Products.ImportProducts;

public sealed record ImportedProduct(
    string Name,
    int Amount,
    decimal CostPrice,
    decimal SewingCost,
    decimal FabricCostAccessoriesCost,
    decimal PackagingCost,
    string Description,
    string ImageFile,
    List<string> Categories);

public sealed record ImportProductsCommand(IReadOnlyList<ImportedProduct> Products) : ICommand<ImportProductsResult>;
public sealed record ImportProductsResult(int ImportedCount);

public sealed class ImportProductsValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsValidator()
    {
        RuleFor(command => command.Products).NotEmpty();
        RuleForEach(command => command.Products).ChildRules(product =>
        {
            product.RuleFor(row => row.Name).NotEmpty();
            product.RuleFor(row => row.Amount).GreaterThanOrEqualTo(0);
            product.RuleFor(row => row.CostPrice).GreaterThanOrEqualTo(0);
            product.RuleFor(row => row.SewingCost).GreaterThanOrEqualTo(0);
            product.RuleFor(row => row.FabricCostAccessoriesCost).GreaterThanOrEqualTo(0);
            product.RuleFor(row => row.PackagingCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class ImportProductsHandler(ICatalogUnitOfWork unitOfWork)
    : ICommandHandler<ImportProductsCommand, ImportProductsResult>
{
    public async Task<ImportProductsResult> Handle(
        ImportProductsCommand command,
        CancellationToken cancellationToken)
    {
        foreach (var row in command.Products)
        {
            unitOfWork.Products.Create(new Product
            {
                Id = Guid.NewGuid(),
                Name = row.Name,
                Amount = row.Amount,
                CostPrice = row.CostPrice,
                SewingCost = row.SewingCost,
                FabricCostAccessoriesCost = row.FabricCostAccessoriesCost,
                PackagingCost = row.PackagingCost,
                Description = row.Description,
                ImageFile = row.ImageFile,
                Categories = row.Categories,
                IsActive = true,
                IsDeleted = false
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ImportProductsResult(command.Products.Count);
    }
}