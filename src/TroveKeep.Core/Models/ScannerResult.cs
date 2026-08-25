namespace TroveKeep.Core.Models;

/// <summary>
/// An entity resolved from a scanned QR code, with its storage allocations resolved
/// to navigable locations (empty for boxes, which route to the box itself).
/// </summary>
public class ScannerResult
{
    public LabelRefKind Kind { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public int? ColorId { get; set; }
    public int Quantity { get; set; }
    // For Storage (neutral QR) results: the physical box/drawer the code points to.
    public StorageType? TargetStorageType { get; set; }
    public Guid? TargetStorageId { get; set; }
    public int? TargetStoragePosition { get; set; }
    public List<ResolvedAllocation> Allocations { get; set; } = [];
}
