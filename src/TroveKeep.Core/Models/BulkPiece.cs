namespace TroveKeep.Core.Models;

public class BulkPiece
{
    public Guid Id { get; set; }
    public string LegoId { get; set; } = string.Empty;
    public int LegoColorId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public List<StorageAllocation> StorageAllocations { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool ImageCached { get; set; } = false;
}
