namespace TroveKeep.Core.Models;

public class Drawer
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public string? Label { get; set; }
    public Guid DrawerContainerId { get; set; }
    public DrawerContainer? DrawerContainer { get; set; }
    public ICollection<BulkPiece> BulkPieces { get; set; } = new List<BulkPiece>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
