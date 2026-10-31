using BuildingBlock.Models;

namespace BuildingBlock.Tests;

public class BaseEntityTests
{
    private sealed class SampleEntity : BaseEntity;

    [Fact]
    public void NewInstance_ExposesIdentityAndDefaultLifecycleTraits()
    {
        var entity = new SampleEntity();

        Assert.Equal(Guid.Empty, entity.Id);
        Assert.Equal(DateTimeOffset.MinValue, entity.CreatedAt);
        Assert.Equal(DateTimeOffset.MinValue, entity.LastModified);
        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.LastModifiedBy);
        Assert.False(entity.IsDeleted);
        Assert.True(entity.IsActive);
    }
}
