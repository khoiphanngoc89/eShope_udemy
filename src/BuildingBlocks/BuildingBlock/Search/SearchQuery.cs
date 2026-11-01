using BuildingBlock.Models;

namespace BuildingBlock.Search;

public sealed record SearchQuery
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? ContainsField { get; init; }

    public string? ContainsValue { get; init; }

    public IReadOnlyDictionary<string, object?> ExactMatches { get; init; } =
        new Dictionary<string, object?>();

    public string? CollectionContainsField { get; init; }

    public string? CollectionContainsValue { get; init; }

    public string OrderBy { get; init; } = nameof(BaseEntity.CreatedAt);

    public OrderDirection OrderDirection { get; init; } = OrderDirection.Desc;
}
