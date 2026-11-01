using BuildingBlock.Models;

namespace BuildingBlock.Tests;

public class PaginatedResultTests
{
    private enum SampleOrderBy
    {
        CreatedAt
    }

    [Fact]
    public void PaginationRequest_WhenConstructedWithoutValues_UsesPageAndSortingDefaults()
    {
        var request = new PaginationRequest<SampleOrderBy>();

        Assert.Equal(1, request.PageNumber);
        Assert.Equal(10, request.PageSize);
        Assert.Equal(SampleOrderBy.CreatedAt, request.OrderBy);
        Assert.Equal(OrderDirection.Desc, request.OrderDirection);
    }

    [Fact]
    public void PaginatedResult_WhenConstructed_HoldsPageFieldsAndItems()
    {
        var items = new[] { "a", "b" };

        var result = new PaginatedResult<string>(1, 10, 25, items, "CreatedAt", OrderDirection.Desc);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(items, result.Items);
        Assert.Equal("CreatedAt", result.OrderBy);
        Assert.Equal(OrderDirection.Desc, result.OrderDirection);
    }
}
