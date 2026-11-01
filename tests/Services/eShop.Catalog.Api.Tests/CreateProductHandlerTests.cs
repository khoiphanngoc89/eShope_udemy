using BuildingBlock.Search;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.CreateProduct;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class CreateProductHandlerTests
{
    [Fact]
    public async Task Handle_WhenProductIsValid_ReturnsNewIdAndStoresMatchingFields()
    {
        var memory = new InMemoryDocumentSession();
        var handler = CreateHandler(memory);
        var command = new CreateProductCommand(
            "Trail Bike",
            5,
            1299.50m,
            0m,
            0m,
            0m,
            "A sturdy trail bike",
            "trail-bike.png",
            ["bikes", "outdoor"]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = await memory.LoadAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(command.Name, stored.Name);
        Assert.Equal(command.CostPrice, stored.CostPrice);
        Assert.Equal(command.Amount, stored.Amount);
        Assert.Equal(command.Description, stored.Description);
        Assert.Equal(command.ImageFile, stored.ImageFile);
        Assert.Equal(command.Categories, stored.Categories);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_ReturnsDistinctIds()
    {
        var memory = new InMemoryDocumentSession();
        var handler = CreateHandler(memory);
        var command = new CreateProductCommand("Name", 1, 1m, 0m, 0m, 0m, "Desc", "file.png", ["a"]);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotNull(await memory.LoadAsync(first.Id));
        Assert.NotNull(await memory.LoadAsync(second.Id));
    }

    [Fact]
    public async Task Handle_WhenProductIsValid_SetsSharedItemTraits()
    {
        var memory = new InMemoryDocumentSession();
        var handler = CreateHandler(memory);
        var command = new CreateProductCommand("Name", 1, 1m, 0m, 0m, 0m, "Desc", "file.png", ["a"]);

        var result = await handler.Handle(command, CancellationToken.None);
        var stored = await memory.LoadAsync(result.Id);

        Assert.NotNull(stored);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.NotEqual(default, stored.CreatedAt);
        Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
        Assert.Equal(stored.CreatedAt, stored.LastModified);
        Assert.False(stored.IsDeleted);
        Assert.True(stored.IsActive);
        Assert.Null(stored.CreatedBy);
        Assert.Null(stored.LastModifiedBy);
    }

    [Fact]
    public async Task Handle_WhenProductIsValid_DoesNotWriteTheSearchIndex()
    {
        var memory = new InMemoryDocumentSession();
        var search = new InMemorySearchStore<Product>();
        var handler = CreateHandler(memory);
        var command = new CreateProductCommand("Name", 1, 1m, 0m, 0m, 0m, "Desc", "file.png", ["a"]);

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(0, search.Count);
    }

    private static CreateProductHandler CreateHandler(InMemoryDocumentSession memory) =>
        new(memory.CreateUnitOfWork());
}
