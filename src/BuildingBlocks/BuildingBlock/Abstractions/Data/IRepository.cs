using BuildingBlock.Models;

namespace BuildingBlock.Abstractions.Data;

public interface IRepository<T> : IRepositoryQueryBase<T> where T : BaseEntity
{
    void Create(T entity);
    void Update(T entity);
    void Delete(T entity);
}
