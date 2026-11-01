using BuildingBlock.Abstractions.Data;
using BuildingBlock.Data;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class UnitOfWorkTests
{
    [Fact]
    public void Repository_WhenCalledTwiceForSameEntity_ReturnsSameInstance()
    {
        var unitOfWork = new UnitOfWork(new InMemoryDocumentSession().CreateSession());

        var first = unitOfWork.Repository<Product>();
        var second = unitOfWork.Repository<Product>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task SaveChangesAsync_CommitsRepositoryCreate()
    {
        var memory = new InMemoryDocumentSession();
        IUnitOfWork unitOfWork = new UnitOfWork(memory.CreateSession());
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Trail Bike",
            CostPrice = 10m,
            Description = "Desc",
            ImageFile = "file.png",
            Categories = ["bikes"]
        };

        unitOfWork.Repository<Product>().Create(product);
        await unitOfWork.SaveChangesAsync();

        var stored = await memory.LoadAsync(product.Id);
        Assert.NotNull(stored);
        Assert.Equal(product.Name, stored.Name);
    }
}
