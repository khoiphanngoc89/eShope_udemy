using BuildingBlock.Models;
using Marten;

namespace BuildingBlock.Data;

public class GenericRepository<T>(IDocumentSession session) : RepositoryQueryBase<T>(session), IRepository<T>
    where T : BaseEntity
{
    public void Create(T entity) => Session.Store(entity);

    public void Update(T entity) => Session.Store(entity);

    public void Delete(T entity)
    {
        entity.IsDeleted = true;
        Session.Store(entity);
    }
}
