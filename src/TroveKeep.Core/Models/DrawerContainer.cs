namespace TroveKeep.Core.Models;

public class DrawerContainer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool ImageCached { get; set; }
    public ICollection<Drawer> Drawers { get; set; } = new List<Drawer>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
