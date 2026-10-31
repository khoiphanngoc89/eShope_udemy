using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Products.UpdateProduct;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class UpdateProductHandlerTests
{
    [Fact]
    public async Task Handle_WhenProductExists_KeepsIdAndCreatedAtAndRefreshesLastModified()
    {
        var memory = new InMemoryDocumentSession();
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-1);
        memory.Store(new Product
        {
            Id = id,
            Name = "Old",
            Price = 1m,
            Description = "Old desc",
            ImageFile = "old.png",
            Categories = ["old"],
            CreatedAt = createdAt,
            LastModified = createdAt,
            IsActive = true,
            IsDeleted = false
        });
        var handler = new UpdateProductHandler(memory.CreateUnitOfWork());
        var command = new UpdateProductCommand(id, "New", 2m, "New desc", "new.png", ["new"]);

        var result = await handler.Handle(command, CancellationToken.None);
        var stored = await memory.LoadAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stored);
        Assert.Equal(id, stored.Id);
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.True(stored.LastModified > createdAt);
        Assert.Equal(command.Name, stored.Name);
        Assert.Equal(command.Price, stored.Price);
        Assert.Equal(command.Description, stored.Description);
        Assert.Equal(command.ImageFile, stored.ImageFile);
        Assert.Equal(command.Categories, stored.Categories);
    }
}
