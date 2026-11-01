using System.Collections;
using System.Reflection;
using BuildingBlock.Exceptions;
using BuildingBlock.Models;

namespace BuildingBlock.Search;

public sealed class InMemorySearchStore<T> : ISearchStore<T> where T : BaseEntity
{
    private readonly Dictionary<Guid, T> _documents = new();

    public bool Unavailable { get; set; }

    public int Count => _documents.Count;

    public bool Contains(Guid id) => _documents.ContainsKey(id);

    public void Store(T document) => UpsertAsync(document).GetAwaiter().GetResult();

    public Task<PaginatedResult<T>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();

        var matches = _documents.Values.Where(document => document is { IsActive: true, IsDeleted: false });
        if (!string.IsNullOrWhiteSpace(query.ContainsField) && !string.IsNullOrWhiteSpace(query.ContainsValue))
        {
            var value = query.ContainsValue.Trim();
            matches = matches.Where(document =>
                ReadString(document, query.ContainsField)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
        }

        foreach (var (field, value) in query.ExactMatches)
        {
            if (value is null)
            {
                continue;
            }

            matches = matches.Where(document => ValuesEqual(ReadProperty(document, field), value));
        }

        if (!string.IsNullOrWhiteSpace(query.CollectionContainsField) &&
            !string.IsNullOrWhiteSpace(query.CollectionContainsValue))
        {
            var value = query.CollectionContainsValue.Trim();
            matches = matches.Where(document =>
                CollectionContains(ReadProperty(document, query.CollectionContainsField), value));
        }

        var ordered = Order(matches, query).ToList();
        var items = ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        return Task.FromResult(new PaginatedResult<T>(
            query.PageNumber,
            query.PageSize,
            ordered.Count,
            items,
            query.OrderBy,
            query.OrderDirection));
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        if (_documents.TryGetValue(id, out var document) && document is { IsActive: true, IsDeleted: false })
        {
            return Task.FromResult<T?>(document);
        }

        return Task.FromResult<T?>(null);
    }

    public Task UpsertAsync(T document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        if (_documents.TryGetValue(document.Id, out var existing) &&
            existing.LastModified.UtcTicks > document.LastModified.UtcTicks)
        {
            return Task.CompletedTask;
        }

        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        _documents.Remove(id);
        return Task.CompletedTask;
    }

    private void EnsureAvailable()
    {
        if (Unavailable)
        {
            throw new SearchIndexUnavailableException("Search index is unavailable");
        }
    }

    private static IEnumerable<T> Order(IEnumerable<T> documents, SearchQuery query)
    {
        var descending = query.OrderDirection != OrderDirection.Asc;
        var property = FindProperty(query.OrderBy);
        Func<T, object?> key = property is null
            ? document => document.CreatedAt
            : document => property.GetValue(document);
        var ordered = descending ? documents.OrderByDescending(key) : documents.OrderBy(key);
        return ordered.ThenBy(document => document.Id);
    }

    private static object? ReadProperty(T document, string name) => FindProperty(name)?.GetValue(document);

    private static string? ReadString(T document, string name) => ReadProperty(document, name)?.ToString();

    private static PropertyInfo? FindProperty(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static bool ValuesEqual(object? actual, object expected)
    {
        if (actual is null)
        {
            return false;
        }

        if (Equals(actual, expected))
        {
            return true;
        }

        try
        {
            return Equals(actual, Convert.ChangeType(expected, actual.GetType()));
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool CollectionContains(object? actual, string value)
    {
        if (actual is IEnumerable items and not string)
        {
            foreach (var item in items)
            {
                if (string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
