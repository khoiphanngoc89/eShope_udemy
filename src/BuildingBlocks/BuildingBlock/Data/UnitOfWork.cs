using BuildingBlock.Abstractions.Data;
using BuildingBlock.Models;
using Marten;

namespace BuildingBlock.Data;

public class UnitOfWork(IDocumentSession session) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
        {
            return (IRepository<T>)existing;
        }

        IRepository<T> repository = new GenericRepository<T>(session);
        _repositories[typeof(T)] = repository;
        return repository;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        session.SaveChangesAsync(cancellationToken);
}
