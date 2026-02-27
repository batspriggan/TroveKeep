namespace TroveKeep.Core.Models;

public class LegoSet
{
    public Guid Id { get; set; }
    public string SetNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public int Quantity { get; set; } = 1;
    public Guid? BoxId { get; set; }
    public Box? Box { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
