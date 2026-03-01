namespace TroveKeep.Core.Models;

public class Room
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public List<PlacedTable> Layout { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
