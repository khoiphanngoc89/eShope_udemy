using BuildingBlock.Data;

namespace BuildingBlock.Tests;

public class QueryableMaterializeExtensionsTests
{
    [Fact]
    public async Task ToMaterializedListAsync_WhenProviderIsLinq_ReturnsItems()
    {
        IQueryable<string> query = new[] { "a", "b" }.AsQueryable();

        var items = await query.ToMaterializedListAsync();

        Assert.Equal(new[] { "a", "b" }, items);
    }
}
