namespace TroveKeep.Core.Models;

public class LegoSet
{
    public Guid Id { get; set; }
    public string SetNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsMoc { get; set; }
    public bool ImageCached { get; set; }
    public List<StorageAllocation> StorageAllocations { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
