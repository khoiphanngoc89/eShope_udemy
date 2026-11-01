using BuildingBlock.Exceptions;
using BuildingBlock.Search;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.GetProducts;

namespace eShop.Catalog.Api.Tests;

public class GetProductsHandlerTests
{
    [Fact]
    public async Task Handle_WhenSearchIndexIsUnavailable_DoesNotQueryTheCatalogStore()
    {
        var store = new InMemorySearchStore<Product> { Unavailable = true };
        var handler = CreateHandler(store);

        await Assert.ThrowsAsync<SearchIndexUnavailableException>(() =>
            handler.Handle(new GetProductsQuery(1, 10), CancellationToken.None));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Handle_WhenMoreProductsThanPageSize_ReturnsOnlyThatPageAndTotalCount()
    {
        var store = new InMemorySearchStore<Product>();
        Seed(store, 5);
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 2), CancellationToken.None);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenStoreIsEmpty_ReturnsEmptyItemsAndZeroTotal()
    {
        var store = new InMemorySearchStore<Product>();
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenPageIsPastTheEnd_ReturnsEmptyItemsAndSameTotal()
    {
        var store = new InMemorySearchStore<Product>();
        Seed(store, 3);
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(5, 2), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenSomeProductsAreInactiveOrDeleted_OmitsThemFromBrowse()
    {
        var store = new InMemorySearchStore<Product>();
        var active = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, isDeleted: false, isActive: true);
        var inactive = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), isDeleted: false, isActive: false);
        var deleted = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), isDeleted: true, isActive: true);
        store.Store(active);
        store.Store(inactive);
        store.Store(deleted);
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(new[] { active.Id }, result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenCalledTwiceAgainstUnchangedStore_ReturnsSameOrder()
    {
        var store = new InMemorySearchStore<Product>();
        var olderId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sameTime = DateTimeOffset.UtcNow;
        store.Store(CreateProduct(olderId, sameTime.AddMinutes(-5), isDeleted: false, isActive: true));
        store.Store(CreateProduct(newerId, sameTime, isDeleted: false, isActive: true));
        var handler = CreateHandler(store);
        var query = new GetProductsQuery(1, 10);

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(new[] { newerId, olderId }, first.Items.Select(item => item.Id));
        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenOrderIsNotSpecified_OrdersByCreatedAtDescending()
    {
        var store = new InMemorySearchStore<Product>();
        var olderCreated = CreateProduct(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            DateTimeOffset.UtcNow,
            name: "Older created");
        olderCreated.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        olderCreated.LastModified = DateTimeOffset.UtcNow;

        var newerCreated = CreateProduct(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            DateTimeOffset.UtcNow.AddDays(-1),
            name: "Newer created");
        newerCreated.CreatedAt = DateTimeOffset.UtcNow;
        newerCreated.LastModified = DateTimeOffset.UtcNow.AddDays(-1);

        store.Store(olderCreated);
        store.Store(newerCreated);
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        Assert.Equal(new[] { newerCreated.Id, olderCreated.Id }, result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenNameIsSupplied_ReturnsCaseInsensitiveContainsMatches()
    {
        var store = new InMemorySearchStore<Product>();
        var trail = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        store.Store(trail);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Helmet", price: 20m, categories: ["gear"]));
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "bike"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([trail.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenVietnameseFullNameIsSupplied_ReturnsThatProduct()
    {
        var store = new InMemorySearchStore<Product>();
        var dress = CreateProduct(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            name: "Đầm Alice - size 1 (Xanh dương)",
            price: 146779m,
            categories: []);
        store.Store(dress);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Đầm Lily size 3 (Trắng phối yếm hồng)", price: 205000m, categories: []));
        var handler = CreateHandler(store);

        var result = await handler.Handle(
            new GetProductsQuery(1, 10, Name: "Đầm Alice - size 1 (Xanh dương)"),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([dress.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenCostPriceIsSupplied_ReturnsExactMatches()
    {
        var store = new InMemorySearchStore<Product>();
        var match = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        store.Store(match);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Road Bike", price: 20m, categories: ["bikes"]));
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10, CostPrice: 10m), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([match.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenCategoryIsSupplied_ReturnsCaseInsensitiveCategoryMatches()
    {
        var store = new InMemorySearchStore<Product>();
        var outdoor = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Tent", price: 50m, categories: ["Outdoor", "camping"]);
        store.Store(outdoor);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Trail Bike", price: 10m, categories: ["bikes"]));
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Category: "outdoor"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([outdoor.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenNamePriceAndCategoryAreSupplied_ReturnsIntersection()
    {
        var store = new InMemorySearchStore<Product>();
        var match = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        store.Store(match);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "City Bike", price: 10m, categories: ["bikes"]));
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), name: "Trail Bike", price: 20m, categories: ["bikes"]));
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-3), name: "Trail Bike", price: 10m, categories: ["gear"]));
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "trail", CostPrice: 10m, Category: "bikes"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([match.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenSearchMatchesInactiveOrDeleted_OmitsThemFromFilteredTotal()
    {
        var store = new InMemorySearchStore<Product>();
        var visible = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        store.Store(visible);
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Trail Bike", price: 10m, categories: ["bikes"], isDeleted: true));
        store.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), name: "Trail Bike", price: 10m, categories: ["bikes"], isActive: false));
        var handler = CreateHandler(store);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "trail"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([visible.Id], result.Items.Select(item => item.Id));
    }

    private static GetProductsHandler CreateHandler(InMemorySearchStore<Product> store) =>
        new(store);

    private static Product CreateProduct(
        Guid id,
        DateTimeOffset lastModified,
        bool isDeleted = false,
        bool isActive = true,
        string? name = null,
        decimal price = 1m,
        List<string>? categories = null) => new()
    {
        Id = id,
        Name = name ?? id.ToString("N")[..8],
        CostPrice = price,
        Description = "Desc",
        ImageFile = "file.png",
        Categories = categories ?? ["bikes"],
        CreatedAt = lastModified,
        LastModified = lastModified,
        IsDeleted = isDeleted,
        IsActive = isActive
    };

    private static void Seed(InMemorySearchStore<Product> store, int count)
    {
        for (var i = 0; i < count; i++)
        {
            store.Store(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product {i}",
                CostPrice = i + 1,
                Description = "Desc",
                ImageFile = "file.png",
                Categories = ["bikes"]
            });
        }
    }
}
