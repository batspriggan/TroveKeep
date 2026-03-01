namespace TroveKeep.Core.Models;

public class PlacedTable
{
    public Guid InstanceId { get; set; }
    public Guid TemplateId { get; set; }
    public int XCm { get; set; }
    public int YCm { get; set; }
}
