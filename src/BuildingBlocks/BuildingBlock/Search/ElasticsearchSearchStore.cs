using BuildingBlock.Exceptions;
using BuildingBlock.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace BuildingBlock.Search;

public sealed class ElasticsearchSearchStore<T> : ISearchStore<T> where T : BaseEntity
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ElasticsearchSearchStore(ElasticsearchOptions options, string indexName)
    {
        _client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(options.Uri)));
        _indexName = indexName;
    }

    public async Task<PaginatedResult<T>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var filters = new List<Elastic.Clients.Elasticsearch.QueryDsl.Query>
            {
                Term("isActive", true),
                Term("isDeleted", false)
            };
            if (!string.IsNullOrWhiteSpace(query.ContainsField) && !string.IsNullOrWhiteSpace(query.ContainsValue))
            {
                filters.Add(ContainsQuery(query.ContainsField, query.ContainsValue));
            }

            foreach (var (field, value) in query.ExactMatches)
            {
                AddExact(filters, Camel(field), value);
            }

            if (!string.IsNullOrWhiteSpace(query.CollectionContainsField) &&
                !string.IsNullOrWhiteSpace(query.CollectionContainsValue))
            {
                filters.Add(Term(Camel(query.CollectionContainsField), query.CollectionContainsValue.Trim().ToLowerInvariant()));
            }

            var response = await _client.SearchAsync<T>(new SearchRequest(_indexName)
            {
                From = (query.PageNumber - 1) * query.PageSize,
                Size = query.PageSize,
                Query = new BoolQuery { Filter = filters }
            }, cancellationToken);

            if (!response.IsValidResponse)
            {
                var type = response.ElasticsearchServerError?.Error.Type;
                var reason = response.ElasticsearchServerError?.Error.Reason;
                // Missing index is an empty catalog, not an outage. See IsMissingIndex.
                if (IsMissingIndex(type, reason))
                {
                    return new PaginatedResult<T>(
                        query.PageNumber,
                        query.PageSize,
                        0,
                        [],
                        query.OrderBy,
                        query.OrderDirection);
                }

                throw new SearchIndexUnavailableException(reason ?? "Search index is unavailable");
            }

            return new PaginatedResult<T>(
                query.PageNumber,
                query.PageSize,
                (int)response.Total,
                response.Documents.ToList(),
                query.OrderBy,
                query.OrderDirection);
        }
        catch (SearchIndexUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SearchIndexUnavailableException("Search index is unavailable", ex);
        }
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetAsync<T>(id, g => g.Index(_indexName), cancellationToken);
            if (!response.IsValidResponse)
            {
                var type = response.ElasticsearchServerError?.Error.Type;
                var reason = response.ElasticsearchServerError?.Error.Reason;
                if (IsMissingIndex(type, reason) || response.ApiCallDetails.HttpStatusCode == 404)
                {
                    return null;
                }

                throw new SearchIndexUnavailableException(reason ?? "Search index is unavailable");
            }

            var document = response.Source;
            return document is { IsActive: true, IsDeleted: false } ? document : null;
        }
        catch (SearchIndexUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SearchIndexUnavailableException("Search index is unavailable", ex);
        }
    }

    public async Task UpsertAsync(T document, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.IndexAsync(new IndexRequest<T>(document, _indexName, document.Id), cancellationToken);
            if (!response.IsValidResponse)
            {
                throw new SearchIndexUnavailableException(response.ElasticsearchServerError?.Error.Reason ?? "Search index is unavailable");
            }
        }
        catch (SearchIndexUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SearchIndexUnavailableException("Search index is unavailable", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteAsync(new DeleteRequest(_indexName, id), cancellationToken);
        }
        catch (Exception ex)
        {
            throw new SearchIndexUnavailableException("Search index is unavailable", ex);
        }
    }

    // Before: wildcard on the analyzed text field (e.g. name).
    // ES standard analyzer splits "Đầm Alice - size 1 (Xanh dương)" into tokens
    // (đầm, alice, size, 1, xanh, dương). A full-name / multi-word contains
    // then matched no token → GET /products?name=... returned totalCount 0.
    // After: case-insensitive contains on the whole string (name.keyword),
    // matching InMemorySearchStore / spec "name contains, ignore case".
    // Accent-folding is not applied: "dam" still does not match "Đầm".
    internal static WildcardQuery ContainsQuery(string field, string value) => new()
    {
        Field = $"{Camel(field)}.keyword",
        Value = $"*{value.Trim()}*",
        CaseInsensitive = true
    };

    // Before: any invalid ES response became SearchIndexUnavailableException (HTTP 503).
    // After import, CDC had not created catalog-products yet, so search returned
    // "no such index [catalog-products]" even though Elasticsearch was up.
    // After: index_not_found is an empty page. Connection / cluster failures stay 503.
    internal static bool IsMissingIndex(string? type, string? reason) =>
        string.Equals(type, "index_not_found_exception", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(reason)
            && reason.Contains("no such index", StringComparison.OrdinalIgnoreCase));

    private static void AddExact(List<Elastic.Clients.Elasticsearch.QueryDsl.Query> filters, string field, object? value)
    {
        if (value is null)
        {
            return;
        }

        filters.Add(value is decimal exact ? Term(field, (double)exact) : Term(field, value));
    }

    private static TermQuery Term(string field, object value) => new()
    {
        Field = field,
        Value = FieldValue.FromValue(value)
    };

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
