using BuildingBlock.Exceptions;
using BuildingBlock.Search;
using eShop.Catalog.Api.Exceptions;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.GetById;

namespace eShop.Catalog.Api.Tests;

public class GetByIdHandlerTests
{
    [Fact]
    public async Task Handle_WhenProductIsInSearchIndex_ReturnsProduct()
    {
        var search = new InMemorySearchStore<Product>();
        var product = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);
        search.Store(product);
        var handler = new GetByIdHandler(search);

        var result = await handler.Handle(new GetByIdQuery(product.Id), CancellationToken.None);

        Assert.Equal(product.Id, result.Product.Id);
        Assert.Equal(product.Name, result.Product.Name);
    }

    [Fact]
    public async Task Handle_WhenProductIsMissing_ThrowsProductNotFound()
    {
        var handler = new GetByIdHandler(new InMemorySearchStore<Product>());

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProductIsDeleted_ThrowsProductNotFound()
    {
        var search = new InMemorySearchStore<Product>();
        var id = Guid.NewGuid();
        search.Store(CreateProduct(id, isDeleted: true, isActive: true));
        var handler = new GetByIdHandler(search);

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ThrowsProductNotFound()
    {
        var search = new InMemorySearchStore<Product>();
        var id = Guid.NewGuid();
        search.Store(CreateProduct(id, isDeleted: false, isActive: false));
        var handler = new GetByIdHandler(search);

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSearchIndexIsUnavailable_ThrowsSearchIndexUnavailable()
    {
        var search = new InMemorySearchStore<Product> { Unavailable = true };
        var handler = new GetByIdHandler(search);

        await Assert.ThrowsAsync<SearchIndexUnavailableException>(() =>
            handler.Handle(new GetByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private static Product CreateProduct(Guid id, bool isDeleted, bool isActive) => new()
    {
        Id = id,
        Name = "Trail Bike",
        CostPrice = 10m,
        Description = "Desc",
        ImageFile = "file.png",
        Categories = ["bikes"],
        IsDeleted = isDeleted,
        IsActive = isActive
    };
}
