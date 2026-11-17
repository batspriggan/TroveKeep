namespace TroveKeep.Core.Models;

/// <summary>
/// A sticker project: a named print job that picks artwork from the global
/// <see cref="StickerImage"/> library and gives every pick a tile footprint, a copy count and a fit.
///
/// <para>
/// <b>Global, not collection-scoped</b> — a project carries no <c>CollectionId</c>, exactly like the
/// image library it draws from. Writes are authorized by <c>ICollectionScope.CanWriteAnywhere</c>
/// through <c>CollectionWriteAnywhereFilter</c>, reads are open to every regular account.
/// </para>
///
/// <para>
/// The items are <b>embedded</b> (no own collection, no joins): a project is always edited and
/// printed as a whole. A project is created first and items are added later, so the list may be
/// empty.
/// </para>
/// </summary>
public class StickerProject
{
    public Guid Id { get; set; }

    /// <summary>Display name, also used as the document title of the printed sheet.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Embedded items, replaced wholesale on every update.</summary>
    public List<StickerItem> Items { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// One entry of a <see cref="StickerProject"/>: which artwork to print, how large (in studs), how
/// many copies, how the artwork is turned inside the label, and how it fills the label.
///
/// <para>
/// The footprint is always expressed in studs — never millimetres and never rotated: neither the
/// fit nor the <see cref="StickerOrientation"/> changes the physical label, they only describe how
/// the artwork fills the label graphic area. The sheet engine decides the page rotation. One stud
/// is <see cref="StudMm"/> millimetres, so a 2×4 tile is a 16 mm × 32 mm label.
/// </para>
/// </summary>
public class StickerItem
{
    /// <summary>Physical size of one stud in millimetres (the LEGO standard the sheet layout uses).</summary>
    public const int StudMm = 8;

    /// <summary>Item-local id (no own collection row): kept across updates so a client can track it.</summary>
    public Guid Id { get; set; }

    /// <summary>Artwork taken from the global sticker image library.</summary>
    public Guid ImageId { get; set; }

    /// <summary>Footprint width in studs (1..32) — 1 stud = 8 mm.</summary>
    public int WidthStuds { get; set; }

    /// <summary>Footprint depth in studs (1..32).</summary>
    public int DepthStuds { get; set; }

    /// <summary>How many labels of this footprint the sheet prints (1..999).</summary>
    public int Copies { get; set; } = 1;

    /// <summary>How the artwork fills the label; defaults to <see cref="StickerFit.Contain"/>.</summary>
    public StickerFit Fit { get; set; } = StickerFit.Contain;

    /// <summary>
    /// How the artwork is turned inside the label (the turning happens <b>before</b> the fit);
    /// defaults to <see cref="StickerOrientation.Auto"/>, i.e. keep the artwork as uploaded.
    /// </summary>
    public StickerOrientation Orientation { get; set; } = StickerOrientation.Auto;

    /// <summary>Free-form remark for the operator (never printed).</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// How an item's artwork fills its label. Orthogonal to the footprint and to the page rotation.
/// </summary>
public enum StickerFit
{
    /// <summary>Whole image visible, letterboxed inside the label (the default).</summary>
    Contain,

    /// <summary>Image scaled until it fills the label, cropped on the overflowing axis, never distorted.</summary>
    Cover,

    /// <summary>Stretched to the label rectangle — the legacy behaviour, which distorts.</summary>
    Fill,

    /// <summary>Natural size, centred, not scaled.</summary>
    Center,
}

/// <summary>
/// How an item's artwork is turned inside its label. Orthogonal to the footprint (the label keeps
/// its millimetres) and to the fit: the artwork is turned first, the fit is applied afterwards, so
/// <see cref="StickerFit.Contain"/> on a landscape image placed <see cref="Portrait"/> letterboxes on
/// the correct axis and <see cref="StickerFit.Cover"/> crops the already-turned image.
///
/// <para>
/// NOT to be confused with the sheet option <c>allowRotate</c>: that one lets the packer turn a
/// whole label by 90° when it is the only way it fits the A4 page, and it stays a layout option.
/// The two rotations are independent and compose.
/// </para>
/// </summary>
public enum StickerOrientation
{
    /// <summary>Keep the artwork's own orientation (the default). Not "no rotation": as uploaded.</summary>
    Auto,

    /// <summary>Force the artwork to lie horizontally (wider than tall).</summary>
    Landscape,

    /// <summary>Force the artwork to stand vertically (taller than wide).</summary>
    Portrait,
}
