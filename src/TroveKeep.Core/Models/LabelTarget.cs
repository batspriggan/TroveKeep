namespace TroveKeep.Core.Models;

/// <summary>
/// Maps a neutral QR/label code to a physical storage location (a box or a drawer).
/// The QR value is opaque; the semantic is resolved through this table, so the association
/// can change (e.g. when a drawer is physically moved) without re-printing the label.
/// </summary>
public class LabelTarget
{
    /// <summary>The exact QR code value (e.g. a UUID, or a legacy code when back-filled).</summary>
    public string Key { get; set; } = string.Empty;
    public StorageType TargetType { get; set; }
    public Guid StorageId { get; set; }
    /// <summary>Position only for drawer targets.</summary>
    public int? StoragePosition { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
