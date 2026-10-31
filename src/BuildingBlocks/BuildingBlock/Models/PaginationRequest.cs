namespace BuildingBlock.Models;

public sealed record PaginationRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
