using BuildingBlock.Data;
using BuildingBlock.Models;
using Marten;

namespace eShop.Catalog.Api.Data;

public sealed class CatalogUnitOfWork(IDocumentSession session, IRepository<Product> products) : ICatalogUnitOfWork
{
    public IRepository<Product> Products { get; } = products;

    public IRepository<T> Repository<T>() where T : BaseEntity =>
        typeof(T) == typeof(Product)
            ? (IRepository<T>)(object)Products
            : throw new InvalidOperationException($"Catalog has no repository for {typeof(T).Name}.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        session.SaveChangesAsync(cancellationToken);
}
