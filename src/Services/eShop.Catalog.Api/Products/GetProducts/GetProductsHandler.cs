using BuildingBlock.Cqrs;
using BuildingBlock.Models;
using eShop.Catalog.Api.Data;
using FluentValidation;

namespace eShop.Catalog.Api.Products.GetProducts;

public sealed record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Name = null,
    decimal? Price = null,
    string? Category = null) : BaseQuery<PaginatedResult<Product>>;

public sealed class GetProductsValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).When(x => x.Price.HasValue);
    }
}

public sealed class GetProductsHandler(ICatalogUnitOfWork unitOfWork) : IQueryHandler<GetProductsQuery, PaginatedResult<Product>>
{
    public Task<PaginatedResult<Product>> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var products = unitOfWork.Products.FindAll();
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim().ToLower();
            products = products.Where(product => product.Name.ToLower().Contains(name));
        }

        if (query.Price is { } price)
        {
            products = products.Where(product => product.Price == price);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim().ToLower();
            products = products.Where(product => product.Categories.Any(item => item.ToLower() == category));
        }

        var matched = products
            .OrderByDescending(product => product.LastModified)
            .ThenBy(product => product.Id)
            .ToList();
        var items = matched
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        return Task.FromResult(new PaginatedResult<Product>(query.PageNumber, query.PageSize, matched.Count, items));
    }
}
