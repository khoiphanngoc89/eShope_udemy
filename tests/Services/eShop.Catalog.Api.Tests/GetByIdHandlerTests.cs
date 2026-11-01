using eShop.Catalog.Api.Exceptions;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.GetById;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class GetByIdHandlerTests
{
    [Fact]
    public async Task Handle_WhenProductIsMissing_ThrowsProductNotFound()
    {
        var memory = new InMemoryDocumentSession();
        var handler = CreateHandler(memory);

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProductIsDeleted_ThrowsProductNotFound()
    {
        var memory = new InMemoryDocumentSession();
        var id = Guid.NewGuid();
        memory.Store(CreateProduct(id, isDeleted: true, isActive: true));
        var handler = CreateHandler(memory);

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ThrowsProductNotFound()
    {
        var memory = new InMemoryDocumentSession();
        var id = Guid.NewGuid();
        memory.Store(CreateProduct(id, isDeleted: false, isActive: false));
        var handler = CreateHandler(memory);

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            handler.Handle(new GetByIdQuery(id), CancellationToken.None));
    }

    private static GetByIdHandler CreateHandler(InMemoryDocumentSession memory) =>
        new(memory.CreateUnitOfWork());

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
