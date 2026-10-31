using BuildingBlock.Models;
using Marten;

namespace BuildingBlock.Data;

public class GenericRepository<T>(IDocumentSession session) : RepositoryQueryBase<T>(session), IRepository<T>
    where T : BaseEntity
{
    public void Create(T entity)
    {
        var now = DateTimeOffset.UtcNow;
        entity.CreatedAt = now;
        entity.LastModified = now;
        Session.Store(entity);
    }

    public void Update(T entity)
    {
        entity.LastModified = DateTimeOffset.UtcNow;
        Session.Store(entity);
    }

    public void Delete(T entity)
    {
        entity.IsDeleted = true;
        Update(entity);
    }
}
