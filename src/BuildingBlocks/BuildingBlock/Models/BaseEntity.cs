namespace BuildingBlock.Models;

public abstract class BaseEntity : IAuditable, IDeletable, IActivable
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;
}
