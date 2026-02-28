namespace TroveKeep.Core.Models;

public class TableTemplate
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public string Color { get; set; } = "#8b6340";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
