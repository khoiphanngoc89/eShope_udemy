using BuildingBlock.Data;

namespace eShop.Catalog.Api.Data;

public interface ICatalogUnitOfWork : IUnitOfWork
{
    IRepository<Product> Products { get; }
}
