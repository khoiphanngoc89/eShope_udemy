using BuildingBlock.Data;
using FluentValidation;
using BuildingBlock.Models;

namespace eShop.Catalog.Api.Products.GetProducts;

public enum ProductOrderBy
{
    CreatedAt,
    LastModified,
    Name,
    CostPrice,
    SewingCost,
    FabricCostAccessoriesCost,
    PackagingCost,
    Amount
}

public sealed record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Name = null,
    decimal? CostPrice = null,
    decimal? SewingCost = null,
    decimal? FabricCostAccessoriesCost = null,
    decimal? PackagingCost = null,
    string? Category = null,
    ProductOrderBy OrderBy = ProductOrderBy.CreatedAt,
    OrderDirection OrderDirection = OrderDirection.Desc) : IQuery<PaginatedResult<Product>>;

public sealed class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.SewingCost).GreaterThanOrEqualTo(0).When(x => x.SewingCost.HasValue);
        RuleFor(x => x.FabricCostAccessoriesCost).GreaterThanOrEqualTo(0).When(x => x.FabricCostAccessoriesCost.HasValue);
        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0).When(x => x.PackagingCost.HasValue);
        RuleFor(x => x.OrderBy).IsInEnum();
        RuleFor(x => x.OrderDirection).IsInEnum();
    }
}

public sealed class GetProductsHandler(ICatalogUnitOfWork unitOfWork) : IQueryHandler<GetProductsQuery, PaginatedResult<Product>>
{
    public async Task<PaginatedResult<Product>> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var products = unitOfWork.Products.FindAll();
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim().ToLower();
            products = products.Where(product => product.Name.ToLower().Contains(name));
        }

        if (query.CostPrice is { } price)
        {
            products = products.Where(product => product.CostPrice == price);
        }

        if (query.SewingCost is { } sewingCost)
        {
            products = products.Where(product => product.SewingCost == sewingCost);
        }

        if (query.FabricCostAccessoriesCost is { } fabricCost)
        {
            products = products.Where(product => product.FabricCostAccessoriesCost == fabricCost);
        }

        if (query.PackagingCost is { } packagingCost)
        {
            products = products.Where(product => product.PackagingCost == packagingCost);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim().ToLower();
            products = products.Where(product => product.Categories.Any(item => item.ToLower() == category));
        }

        var matched = await products
            .OrderByDescending(product => product.LastModified)
            .ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);
        var items = matched
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        return new PaginatedResult<Product>(query.PageNumber, query.PageSize, matched.Count, items);
    }
}
