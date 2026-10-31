using BuildingBlock.Models;

namespace BuildingBlock.Tests;

public class PaginatedResultTests
{
    [Fact]
    public void PaginationRequest_WhenConstructedWithoutValues_UsesPageOneAndSizeTen()
    {
        var request = new PaginationRequest();

        Assert.Equal(1, request.PageNumber);
        Assert.Equal(10, request.PageSize);
    }

    [Fact]
    public void PaginatedResult_WhenConstructed_HoldsPageFieldsAndItems()
    {
        var items = new[] { "a", "b" };

        var result = new PaginatedResult<string>(1, 10, 25, items);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(items, result.Items);
    }
}
