using BuildingBlock.Data;
using eShop.Catalog.Api.Models;
using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class GenericRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenActiveAndNotDeleted_ReturnsEntity()
    {
        var memory = new InMemoryDocumentSession();
        var product = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);
        memory.Store(product);
        var repository = new GenericRepository<Product>(memory.CreateSession());

        var loaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(product.Id, loaded.Id);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task GetByIdAsync_WhenInactiveOrDeleted_ReturnsNull(bool isDeleted, bool isActive)
    {
        var memory = new InMemoryDocumentSession();
        var product = CreateProduct(Guid.NewGuid(), isDeleted, isActive);
        memory.Store(product);
        var repository = new GenericRepository<Product>(memory.CreateSession());

        var loaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public void FindAll_AppliesBaseQueryIsActiveAndNotDeleted()
    {
        var memory = new InMemoryDocumentSession();
        var visible = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);
        memory.Store(visible);
        memory.Store(CreateProduct(Guid.NewGuid(), isDeleted: true, isActive: true));
        memory.Store(CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: false));
        var repository = new GenericRepository<Product>(memory.CreateSession());

        var items = repository.FindAll().ToList();

        Assert.Equal([visible.Id], items.Select(item => item.Id));
    }

    [Fact]
    public void FindByCondition_AppliesBaseQueryThenPredicate()
    {
        var memory = new InMemoryDocumentSession();
        var visibleMatch = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true, name: "Trail Bike");
        memory.Store(visibleMatch);
        memory.Store(CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true, name: "Road Bike"));
        memory.Store(CreateProduct(Guid.NewGuid(), isDeleted: true, isActive: true, name: "Trail Bike"));
        memory.Store(CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: false, name: "Trail Bike"));
        var repository = new GenericRepository<Product>(memory.CreateSession());

        var items = repository.FindByCondition(product => product.Name == "Trail Bike").ToList();

        Assert.Equal([visibleMatch.Id], items.Select(item => item.Id));
    }

    [Fact]
    public async Task Create_ThenSaveChanges_PersistsEntity()
    {
        var memory = new InMemoryDocumentSession();
        var session = memory.CreateSession();
        var repository = new GenericRepository<Product>(session);
        var unitOfWork = new UnitOfWork(session);
        var product = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);

        repository.Create(product);
        await unitOfWork.SaveChangesAsync();

        var stored = await memory.LoadAsync(product.Id);
        Assert.NotNull(stored);
        Assert.Equal(product.Name, stored.Name);
        Assert.NotEqual(DateTimeOffset.MinValue, stored.CreatedAt);
        Assert.Equal(stored.CreatedAt, stored.LastModified);
        Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
    }

    [Fact]
    public async Task Update_ThenSaveChanges_PreservesCreationAuditAndRefreshesModificationTime()
    {
        var memory = new InMemoryDocumentSession();
        var session = memory.CreateSession();
        var repository = new GenericRepository<Product>(session);
        var unitOfWork = new UnitOfWork(session);
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var product = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);
        product.CreatedAt = createdAt;
        product.LastModified = createdAt;
        product.CreatedBy = "creator";
        memory.Store(product);

        product.Name = "Updated";
        repository.Update(product);
        await unitOfWork.SaveChangesAsync();

        var stored = await memory.LoadAsync(product.Id);
        Assert.NotNull(stored);
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Equal("creator", stored.CreatedBy);
        Assert.True(stored.LastModified > createdAt);
        Assert.Equal(TimeSpan.Zero, stored.LastModified.Offset);
    }

    [Fact]
    public async Task Delete_ThenSaveChanges_MarksEntityDeleted()
    {
        var memory = new InMemoryDocumentSession();
        var product = CreateProduct(Guid.NewGuid(), isDeleted: false, isActive: true);
        memory.Store(product);
        var session = memory.CreateSession();
        var repository = new GenericRepository<Product>(session);
        var unitOfWork = new UnitOfWork(session);

        repository.Delete(product);
        await unitOfWork.SaveChangesAsync();

        var stored = await memory.LoadAsync(product.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsDeleted);
        Assert.NotEqual(DateTimeOffset.MinValue, stored.LastModified);
        Assert.Null(await repository.GetByIdAsync(product.Id, CancellationToken.None));
    }

    private static Product CreateProduct(Guid id, bool isDeleted, bool isActive, string name = "Trail Bike") => new()
    {
        Id = id,
        Name = name,
        Price = 10m,
        Description = "Desc",
        ImageFile = "file.png",
        Categories = ["bikes"],
        IsDeleted = isDeleted,
        IsActive = isActive
    };
}
