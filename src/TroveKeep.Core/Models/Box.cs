namespace TroveKeep.Core.Models;

public class Box
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool ImageCached { get; set; }
    public ICollection<LegoSet> Sets { get; set; } = new List<LegoSet>();
    public ICollection<BulkPiece> BulkPieces { get; set; } = new List<BulkPiece>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
