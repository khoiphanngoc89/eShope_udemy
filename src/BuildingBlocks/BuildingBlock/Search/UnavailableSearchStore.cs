using BuildingBlock.Exceptions;
using BuildingBlock.Models;

namespace BuildingBlock.Search;

public sealed class UnavailableSearchStore<T> : ISearchStore<T> where T : BaseEntity
{
    public Task<PaginatedResult<T>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException("Search index is unavailable");

    public Task UpsertAsync(T document, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException("Search index is unavailable");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new SearchIndexUnavailableException("Search index is unavailable");
}
