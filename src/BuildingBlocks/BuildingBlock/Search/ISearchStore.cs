using BuildingBlock.Models;

namespace BuildingBlock.Search;

public interface ISearchStore<T> where T : BaseEntity
{
    Task<PaginatedResult<T>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    Task UpsertAsync(T document, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
