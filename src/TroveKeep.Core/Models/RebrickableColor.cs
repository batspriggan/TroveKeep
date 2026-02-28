namespace TroveKeep.Core.Models;

public class RebrickableColor
{
    public Guid UniqueId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Rgb { get; set; } = "";
    public bool IsTrans { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
}
