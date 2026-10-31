using BuildingBlock.Data;
using eShop.Catalog.Api.Data;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class CatalogUnitOfWorkTests
{
    [Fact]
    public void Products_HoldsInjectedProductRepository()
    {
        var session = new InMemoryDocumentSession().CreateSession();
        IRepository<Product> products = new GenericRepository<Product>(session);
        var unitOfWork = new CatalogUnitOfWork(session, products);

        Assert.Same(products, unitOfWork.Products);
        Assert.Same(products, unitOfWork.Repository<Product>());
    }

    [Fact]
    public async Task SaveChangesAsync_CommitsProductsCreate()
    {
        var memory = new InMemoryDocumentSession();
        var session = memory.CreateSession();
        ICatalogUnitOfWork unitOfWork = new CatalogUnitOfWork(session, new GenericRepository<Product>(session));
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Trail Bike",
            Price = 10m,
            Description = "Desc",
            ImageFile = "file.png",
            Categories = ["bikes"]
        };

        unitOfWork.Products.Create(product);
        await unitOfWork.SaveChangesAsync();

        var stored = await memory.LoadAsync(product.Id);
        Assert.NotNull(stored);
        Assert.Equal(product.Name, stored.Name);
    }
}
