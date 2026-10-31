using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.GetProducts;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class GetProductsHandlerTests
{
    [Fact]
    public async Task Handle_WhenMoreProductsThanPageSize_ReturnsOnlyThatPageAndTotalCount()
    {
        var memory = new InMemoryDocumentSession();
        Seed(memory, 5);
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 2), CancellationToken.None);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenStoreIsEmpty_ReturnsEmptyItemsAndZeroTotal()
    {
        var memory = new InMemoryDocumentSession();
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenPageIsPastTheEnd_ReturnsEmptyItemsAndSameTotal()
    {
        var memory = new InMemoryDocumentSession();
        Seed(memory, 3);
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(5, 2), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WhenSomeProductsAreInactiveOrDeleted_OmitsThemFromBrowse()
    {
        var memory = new InMemoryDocumentSession();
        var active = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, isDeleted: false, isActive: true);
        var inactive = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), isDeleted: false, isActive: false);
        var deleted = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), isDeleted: true, isActive: true);
        memory.Store(active);
        memory.Store(inactive);
        memory.Store(deleted);
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(new[] { active.Id }, result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenCalledTwiceAgainstUnchangedStore_ReturnsSameOrder()
    {
        var memory = new InMemoryDocumentSession();
        var olderId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sameTime = DateTimeOffset.UtcNow;
        memory.Store(CreateProduct(olderId, sameTime.AddMinutes(-5), isDeleted: false, isActive: true));
        memory.Store(CreateProduct(newerId, sameTime, isDeleted: false, isActive: true));
        var handler = CreateHandler(memory);
        var query = new GetProductsQuery(1, 10);

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(new[] { newerId, olderId }, first.Items.Select(item => item.Id));
        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenNameIsSupplied_ReturnsCaseInsensitiveContainsMatches()
    {
        var memory = new InMemoryDocumentSession();
        var trail = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        memory.Store(trail);
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Helmet", price: 20m, categories: ["gear"]));
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "bike"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([trail.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenPriceIsSupplied_ReturnsExactPriceMatches()
    {
        var memory = new InMemoryDocumentSession();
        var match = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        memory.Store(match);
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Road Bike", price: 20m, categories: ["bikes"]));
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Price: 10m), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([match.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenCategoryIsSupplied_ReturnsCaseInsensitiveCategoryMatches()
    {
        var memory = new InMemoryDocumentSession();
        var outdoor = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Tent", price: 50m, categories: ["Outdoor", "camping"]);
        memory.Store(outdoor);
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Trail Bike", price: 10m, categories: ["bikes"]));
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Category: "outdoor"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([outdoor.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenNamePriceAndCategoryAreSupplied_ReturnsIntersection()
    {
        var memory = new InMemoryDocumentSession();
        var match = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        memory.Store(match);
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "City Bike", price: 10m, categories: ["bikes"]));
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), name: "Trail Bike", price: 20m, categories: ["bikes"]));
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-3), name: "Trail Bike", price: 10m, categories: ["gear"]));
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "trail", Price: 10m, Category: "bikes"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([match.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Handle_WhenSearchMatchesInactiveOrDeleted_OmitsThemFromFilteredTotal()
    {
        var memory = new InMemoryDocumentSession();
        var visible = CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow, name: "Trail Bike", price: 10m, categories: ["bikes"]);
        memory.Store(visible);
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), name: "Trail Bike", price: 10m, categories: ["bikes"], isDeleted: true));
        memory.Store(CreateProduct(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2), name: "Trail Bike", price: 10m, categories: ["bikes"], isActive: false));
        var handler = CreateHandler(memory);

        var result = await handler.Handle(new GetProductsQuery(1, 10, Name: "trail"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal([visible.Id], result.Items.Select(item => item.Id));
    }

    private static GetProductsHandler CreateHandler(InMemoryDocumentSession memory) =>
        new(memory.CreateUnitOfWork());

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
        Price = price,
        Description = "Desc",
        ImageFile = "file.png",
        Categories = categories ?? ["bikes"],
        CreatedAt = lastModified,
        LastModified = lastModified,
        IsDeleted = isDeleted,
        IsActive = isActive
    };

    private static void Seed(InMemoryDocumentSession memory, int count)
    {
        for (var i = 0; i < count; i++)
        {
            memory.Store(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product {i}",
                Price = i + 1,
                Description = "Desc",
                ImageFile = "file.png",
                Categories = ["bikes"]
            });
        }
    }
}
