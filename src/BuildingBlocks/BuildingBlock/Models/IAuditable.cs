namespace BuildingBlock.Models;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset LastModified { get; set; }
    string? CreatedBy { get; set; }
    string? LastModifiedBy { get; set; }
}
