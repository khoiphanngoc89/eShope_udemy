using BuildingBlock.Abstractions.Data;
using BuildingBlock.Data;

namespace eShop.Catalog.Api.Abstractions;

public interface ICatalogUnitOfWork : IUnitOfWork
{
    IRepository<Product> Products { get; }
}
