using System.Text.Json.Serialization;

namespace BuildingBlock.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderDirection
{
    Asc,
    Desc
}

public sealed record PaginationRequest<TOrderBy> where TOrderBy : struct, Enum
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public TOrderBy OrderBy { get; init; }
    public OrderDirection OrderDirection { get; init; } = OrderDirection.Desc;
}
