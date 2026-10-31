namespace BuildingBlock.Models;

public sealed record PaginatedResult<T>(int PageNumber, int PageSize, int TotalCount, IReadOnlyList<T> Items);
