namespace TroveKeep.Core.Models;

public class StorageAllocation
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid StorageId { get; set; }
    public StorageType StorageType { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
