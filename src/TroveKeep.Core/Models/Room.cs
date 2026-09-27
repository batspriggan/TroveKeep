namespace TroveKeep.Core.Models;

public class Room
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }

    /// <summary>
    /// Archived room: kept for reference but hidden from the room list and excluded from
    /// planning maths (e.g. the build check). "Obsolete" means superseded in practice, not
    /// versioned: a replacement is created by duplicating the room.
    /// </summary>
    public bool Obsolete { get; set; }

    public List<PlacedTable> Layout { get; set; } = [];
    public List<AggregateSelection> AggregateSelections { get; set; } = [];
    public List<AggregateBpLayout> AggregateBpLayouts { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Smallest room that contains a layout, in whole centimetres. Reported to the client so it can
/// block a dimension that could never be saved.
/// </summary>
public record RoomBounds(int WidthCm, int DepthCm, double OffsetXCm, double OffsetYCm);
