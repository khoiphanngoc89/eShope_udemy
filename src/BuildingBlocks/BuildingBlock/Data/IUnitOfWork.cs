using BuildingBlock.Models;

namespace BuildingBlock.Data;

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
