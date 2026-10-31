namespace BuildingBlock.Models;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime LastModified { get; set; }
    string? CreatedBy { get; set; }
    string? LastModifiedBy { get; set; }
}
