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
/// One entry of a <see cref="StickerProject"/>: which artwork to print, how large, how many copies,
/// how the artwork is turned inside the label, and how it fills the label.
///
/// <para>
/// <b>Two mounts, both expressed in studs on the first axis</b> (<see cref="StickerMount"/>):
/// <list type="bullet">
/// <item><see cref="StickerMount.Tile"/> — a flat piece: the label is
/// <see cref="WidthStuds"/> × <see cref="DepthStuds"/> studs, one stud being <see cref="StudMm"/> mm,
/// so a 2×4 tile is a 16 mm × 32 mm label.</item>
/// <item><see cref="StickerMount.BrickStack"/> — the long side of a stack of bricks: the label is
/// <see cref="WidthStuds"/> studs long and <see cref="BrickHeight"/> bricks high, one brick being
/// <see cref="BrickMm"/> mm, so a 1×8 on 6 bricks is a 64 mm × 57.6 mm label. The depth is
/// conceptually one stud and is forced to 1: only the facade is printed.</item>
/// </list>
/// </para>
///
/// <para>
/// The label is never rotated: neither the fit nor the <see cref="StickerOrientation"/> changes the
/// physical label, they only describe how the artwork fills the label graphic area. The sheet
/// engine decides the page rotation.
/// </para>
/// </summary>
public class StickerItem
{
    /// <summary>Physical size of one stud in millimetres (the LEGO standard the sheet layout uses).</summary>
    public const int StudMm = 8;

    /// <summary>Height of one brick in millimetres — the second axis of a <see cref="StickerMount.BrickStack"/> label.</summary>
    public const decimal BrickMm = 9.6m;

    /// <summary>Item-local id (no own collection row): kept across updates so a client can track it.</summary>
    public Guid Id { get; set; }

    /// <summary>Artwork taken from the global sticker image library.</summary>
    public Guid ImageId { get; set; }

    /// <summary>
    /// What the label is stuck on; defaults to <see cref="StickerMount.Tile"/>, so an item stored
    /// before the mounts existed deserialises unchanged.
    /// </summary>
    public StickerMount Mount { get; set; } = StickerMount.Tile;

    /// <summary>Width in studs (1..32) — 1 stud = 8 mm. For a brick stack this is the facade length.</summary>
    public int WidthStuds { get; set; }

    /// <summary>Footprint depth in studs (1..32) of a tile; always 1 for a brick stack.</summary>
    public int DepthStuds { get; set; }

    /// <summary>
    /// Height of a brick stack in bricks (2 decimal places, &gt; 0); <c>null</c> for a tile. The user
    /// decides the value — it is not derived from a piece type and is not checked against real LEGO
    /// heights (a stack may be 2 + 1/3 bricks).
    /// </summary>
    public decimal? BrickHeight { get; set; }

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
/// What an item's label is mounted on. Orthogonal to the fit and to the artwork orientation, and
/// the only thing that decides the second label dimension.
/// </summary>
public enum StickerMount
{
    /// <summary>A flat piece: <see cref="StickerItem.WidthStuds"/> × <see cref="StickerItem.DepthStuds"/> studs (the default).</summary>
    Tile,

    /// <summary>
    /// The long side of a stack of bricks: <see cref="StickerItem.WidthStuds"/> studs ×
    /// <see cref="StickerItem.BrickHeight"/> bricks. <see cref="StickerItem.DepthStuds"/> is ignored
    /// and forced to 1 (the stack is one stud deep as far as the label is concerned).
    /// </summary>
    BrickStack,
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
