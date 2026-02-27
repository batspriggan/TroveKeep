namespace TroveKeep.Core.Models;

public class StorageAllocation
{
    public Guid StorageId { get; set; }
    public StorageType Type { get; set; }
    public int Quantity { get; set; }
}
