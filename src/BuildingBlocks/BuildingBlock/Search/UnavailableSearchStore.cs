using BuildingBlock.Exceptions;
using BuildingBlock.Models;

namespace BuildingBlock.Search;

public sealed class UnavailableSearchStore<T> : ISearchStore<T> where T : BaseEntity
{
    public Task<PaginatedResult<T>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException();

    public Task UpsertAsync(T document, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException();
}
