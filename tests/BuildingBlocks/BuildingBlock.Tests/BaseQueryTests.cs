using BuildingBlock.Cqrs;
using BuildingBlock.Models;

namespace BuildingBlock.Tests;

public class BaseQueryTests
{
    private sealed record SampleQuery() : BaseQuery<string>;

    private sealed class SampleEntity : BaseEntity;

    [Fact]
    public void SampleQuery_IsIQuery()
    {
        IQuery<string> query = new SampleQuery();

        Assert.NotNull(query);
    }

    [Fact]
    public void Matches_WhenActiveAndNotDeleted_ReturnsTrue()
    {
        var entity = new SampleEntity { IsActive = true, IsDeleted = false };

        Assert.True(BaseQuery.Matches(entity));
    }

    [Fact]
    public void Matches_WhenInactiveOrDeletedOrNull_ReturnsFalse()
    {
        Assert.False(BaseQuery.Matches<SampleEntity>(null));
        Assert.False(BaseQuery.Matches(new SampleEntity { IsActive = false, IsDeleted = false }));
        Assert.False(BaseQuery.Matches(new SampleEntity { IsActive = true, IsDeleted = true }));
    }

    [Fact]
    public void Filter_WhenMixedEntities_KeepsOnlyActiveAndNotDeleted()
    {
        var visible = new SampleEntity { Id = Guid.NewGuid(), IsActive = true, IsDeleted = false };
        var inactive = new SampleEntity { Id = Guid.NewGuid(), IsActive = false, IsDeleted = false };
        var deleted = new SampleEntity { Id = Guid.NewGuid(), IsActive = true, IsDeleted = true };

        var result = new[] { visible, inactive, deleted }.AsQueryable().ApplyBaseQuery().ToList();

        Assert.Equal([visible.Id], result.Select(item => item.Id));
    }
}
