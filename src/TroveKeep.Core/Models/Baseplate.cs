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
    public RoadShape? RoadShape { get; set; }
    public int Quantity { get; set; } = 1;
    public List<BaseplateReservation> Reservations { get; set; } = [];
    public string? Notes { get; set; }
    public bool NeedsReview { get; set; }

    /// <summary>
    /// True when a legacy MOC row (<c>Custom</c> + <c>LinkedSetId</c>) could not be converted
    /// automatically. The row stays in the catalogue but is not placeable until reconciled.
    /// </summary>
    public bool Quarantined { get; set; }

    /// <summary>Human-readable reason for <see cref="Quarantined"/>.</summary>
    public string? QuarantineReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
