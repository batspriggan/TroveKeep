namespace TroveKeep.Core.Models;

public class BulkPiece
{
    public Guid Id { get; set; }
    public string LegoId { get; set; } = string.Empty;
    public string LegoColor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? BoxId { get; set; }
    public Box? Box { get; set; }
    public Guid? DrawerId { get; set; }
    public Drawer? Drawer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
