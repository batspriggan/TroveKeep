namespace TroveKeep.Core.Models;

public enum BaseplateType { Standard, Road, Custom }

public class Baseplate
{
    public Guid Id { get; set; }
    public BaseplateType Type { get; set; } = BaseplateType.Standard;
    public string PartNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public int LegoColorId { get; set; }
    public bool ImageCached { get; set; }
    public Guid? LinkedSetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
