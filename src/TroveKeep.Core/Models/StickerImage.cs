namespace TroveKeep.Core.Models;

/// <summary>
/// One piece of printable artwork of the global sticker image library: the picture the operator
/// wants on a LEGO tile, with no size of its own.
///
/// <para>
/// <b>Global, not collection-scoped.</b> Like the sticker projects that use it, an image carries no
/// <see cref="CollectionId"/>: the library is one shelf of artwork shared by the whole instance
/// (a sheet mixes items that belong to different projects and collections). Writes are therefore
/// not authorized by a target collection's access level but by "may write anywhere" — see
/// <c>ICollectionScope.CanWriteAnywhere</c> and <c>CollectionWriteAnywhereFilter</c>.
/// </para>
///
/// <para>
/// The physical footprint lives on <see cref="StickerItem"/>, not here: the same artwork can be
/// printed as a 1×1 or a 2×4 tile. The bytes are stored through
/// <see cref="ImageReferenceType.StickerImage"/> keyed by the image id, and
/// <see cref="ImageCached"/> tells clients whether fetching the artwork is worth a request.
/// </para>
/// </summary>
public class StickerImage
{
    public Guid Id { get; set; }

    /// <summary>Display name, also used as the artwork's label in the library UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>True when an image has been uploaded for this library entry.</summary>
    public bool ImageCached { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
