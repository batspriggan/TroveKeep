namespace TroveKeep.Core.Models;

/// <summary>
/// A bulk piece resolved from a scanned QR code, with its storage allocations resolved
/// to navigable locations.
/// </summary>
public class ScannerResult
{
    public Guid Id { get; set; }
    public string LegoId { get; set; } = string.Empty;
    public int LegoColorId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<ResolvedAllocation> Allocations { get; set; } = [];
}
