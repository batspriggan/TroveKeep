namespace TroveKeep.Core.Models;

public class PlacedBaseplate
{
    public Guid InstanceId { get; set; }
    public Guid BaseplateId { get; set; }
    public int XMm { get; set; }        // mm relative to aggregate bounding-box origin
    public int YMm { get; set; }
    public int Rotation { get; set; }   // 0 | 90 | 180 | 270

    /// <summary>MOC/set this placement originates from; null for individually placed plates.</summary>
    public Guid? SourceSetId { get; set; }
    public Guid? PlacementId { get; set; }   // identifies this placed instance (a MOC can be placed more than once)
}
