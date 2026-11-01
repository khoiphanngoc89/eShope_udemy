using System.Linq.Expressions;
using BuildingBlock.Abstractions;
using BuildingBlock.Abstractions.Data;
using BuildingBlock.Models;
using Marten;

namespace BuildingBlock.Data;

public class RepositoryQueryBase<T>(IDocumentSession session) : IRepositoryQueryBase<T>
    where T : BaseEntity, IActivable, IDeletable
{
    protected IDocumentSession Session { get; } = session;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Session.LoadAsync<T>(id, cancellationToken);
        return Query.Matches(entity) ? entity : null;
    }

    public IQueryable<T> FindAll() => Session.Query<T>().ApplyQuery();

    public IQueryable<T> FindByCondition(Expression<Func<T, bool>> condition) =>
        FindAll().Where(condition);
}
