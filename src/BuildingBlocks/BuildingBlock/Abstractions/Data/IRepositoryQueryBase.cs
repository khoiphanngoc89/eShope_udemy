using System.Linq.Expressions;
using BuildingBlock.Models;

namespace BuildingBlock.Abstractions.Data;

public interface IRepositoryQueryBase<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<T> FindAll();
    IQueryable<T> FindByCondition(Expression<Func<T, bool>> condition);
}
