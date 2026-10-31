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
        Assert.Equal(default, entity.CreatedAt);
        Assert.Equal(default, entity.LastModified);
        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.LastModifiedBy);
        Assert.False(entity.IsDeleted);
        Assert.True(entity.IsActive);
    }
}
