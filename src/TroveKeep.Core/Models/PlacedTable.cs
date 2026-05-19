namespace TroveKeep.Core.Models;

public class PlacedTable
{
    public Guid InstanceId { get; set; }
    public Guid TemplateId { get; set; }
    public double XCm { get; set; }
    public double YCm { get; set; }
}
