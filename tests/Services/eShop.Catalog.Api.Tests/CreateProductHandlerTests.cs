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
        var handler = new CreateProductHandler(memory.CreateSession());
        var command = new CreateProductCommand(
            "Trail Bike",
            1299.50m,
            "A sturdy trail bike",
            "trail-bike.png",
            ["bikes", "outdoor"]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = await memory.LoadAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(command.Name, stored.Name);
        Assert.Equal(command.Price, stored.Price);
        Assert.Equal(command.Description, stored.Description);
        Assert.Equal(command.ImageFile, stored.ImageFile);
        Assert.Equal(command.Categories, stored.Categories);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_ReturnsDistinctIds()
    {
        var memory = new InMemoryDocumentSession();
        var handler = new CreateProductHandler(memory.CreateSession());
        var command = new CreateProductCommand("Name", 1m, "Desc", "file.png", ["a"]);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotNull(await memory.LoadAsync(first.Id));
        Assert.NotNull(await memory.LoadAsync(second.Id));
    }
}
