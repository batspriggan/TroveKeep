namespace TroveKeep.Core.Models;

public class SearchResult
{
    public IEnumerable<SetSearchResult> Sets { get; set; } = [];
    public IEnumerable<PieceSearchResult> Pieces { get; set; } = [];
}

public class SetSearchResult
{
    public Guid Id { get; set; }
    public string SetNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<ResolvedAllocation> Allocations { get; set; } = [];
}

public class PieceSearchResult
{
    public Guid Id { get; set; }
    public string LegoId { get; set; } = string.Empty;
    public string LegoColor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<ResolvedAllocation> Allocations { get; set; } = [];
}

public class ResolvedAllocation
{
    public Guid StorageId { get; set; }
    public StorageType StorageType { get; set; }
    public string StorageName { get; set; } = string.Empty;
    public Guid? DrawerContainerId { get; set; }
    public string? DrawerContainerName { get; set; }
    public int? DrawerPosition { get; set; }
    public int Quantity { get; set; }
}
