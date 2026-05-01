namespace TroveKeep.Core.Models;

public class PlacedBaseplate
{
    public Guid InstanceId { get; set; }
    public Guid BaseplateId { get; set; }
    public int XMm { get; set; }        // mm relative to aggregate bounding-box origin
    public int YMm { get; set; }
    public int Rotation { get; set; }   // 0 | 90 | 180 | 270
}
