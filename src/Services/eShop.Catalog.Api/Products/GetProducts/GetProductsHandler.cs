using System.Diagnostics;
using BuildingBlock.Exceptions;
using BuildingBlock.Models;
using BuildingBlock.Search;
using eShop.Catalog.Api.Search;
using FluentValidation;

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

public sealed class GetProductsHandler(ISearchStore<Product> searchStore) : IQueryHandler<GetProductsQuery, PaginatedResult<Product>>
{
    public async Task<PaginatedResult<Product>> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await searchStore.SearchAsync(query.ToSearchQuery(), cancellationToken);
            CatalogSearchMetrics.RecordDuration(stopwatch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (SearchIndexUnavailableException)
        {
            CatalogSearchMetrics.RecordDuration(stopwatch.Elapsed.TotalMilliseconds);
            CatalogSearchMetrics.RecordError();
            throw;
        }
        catch (Exception ex)
        {
            CatalogSearchMetrics.RecordDuration(stopwatch.Elapsed.TotalMilliseconds);
            CatalogSearchMetrics.RecordError();
            throw new SearchIndexUnavailableException("Search index is unavailable", ex);
        }
    }
}

internal static class GetProductsQuerySearchMapping
{
    public static SearchQuery ToSearchQuery(this GetProductsQuery query)
    {
        var exact = new Dictionary<string, object?>();
        if (query.CostPrice is { } costPrice)
        {
            exact[nameof(Product.CostPrice)] = costPrice;
        }

        if (query.SewingCost is { } sewingCost)
        {
            exact[nameof(Product.SewingCost)] = sewingCost;
        }

        if (query.FabricCostAccessoriesCost is { } fabricCost)
        {
            exact[nameof(Product.FabricCostAccessoriesCost)] = fabricCost;
        }

        if (query.PackagingCost is { } packagingCost)
        {
            exact[nameof(Product.PackagingCost)] = packagingCost;
        }

        return new SearchQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            ContainsField = string.IsNullOrWhiteSpace(query.Name) ? null : nameof(Product.Name),
            ContainsValue = query.Name,
            ExactMatches = exact,
            CollectionContainsField = string.IsNullOrWhiteSpace(query.Category) ? null : nameof(Product.Categories),
            CollectionContainsValue = query.Category,
            OrderBy = query.OrderBy.ToString(),
            OrderDirection = query.OrderDirection
        };
    }
}
