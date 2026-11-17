namespace TroveKeep.Core.Models;

/// <summary>
/// A printable sticker of the global sticker library: the artwork the operator wants on a LEGO
/// tile, plus the tile footprint (in studs) it is cut for.
///
/// <para>
/// <b>Global, not collection-scoped.</b> Unlike every other domain row a sticker carries no
/// <see cref="CollectionId"/>: the library is one shelf of artwork shared by the whole instance
/// (an A4 label sheet mixes stickers used in different collections). Writes are therefore not
/// authorized by a target collection's access level but by "may write anywhere" — see
/// <c>ICollectionScope.CanWriteAnywhere</c> and <c>CollectionWriteAnywhereFilter</c>.
/// </para>
///
/// <para>
/// The physical size of the sticker is derived from the footprint: one stud is 8 mm, so a 2×4
/// tile is a 16 mm × 32 mm label. The artwork itself is stored through
/// <see cref="ImageReferenceType.Sticker"/> keyed by the sticker id, and
/// <see cref="ImageCached"/> tells clients whether fetching the image is worth a request.
/// </para>
/// </summary>
public class Sticker
{
    /// <summary>Physical size of one stud in millimetres (the LEGO standard the sheet layout uses).</summary>
    public const int StudMm = 8;

    public Guid Id { get; set; }

    /// <summary>Display name, also used as the artwork's label in the sheet plan.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Footprint width in studs (1..32) — 1 stud = 8 mm.</summary>
    public int WidthStuds { get; set; }

    /// <summary>Footprint depth in studs (1..32).</summary>
    public int DepthStuds { get; set; }

    /// <summary>True when an image has been uploaded for this sticker.</summary>
    public bool ImageCached { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
