using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// The shared business limits of the sticker feature: one name rule for the image library and the
/// projects, the stud range of an item footprint, the copy range, the printable label bound, and the
/// fit/orientation/mount parsers. Kept in one place so the image and project services cannot drift
/// apart.
/// </summary>
public static class StickerRules
{
    public const int MaxNameLength = 80;
    public const int MinStuds = 1;
    public const int MaxStuds = 32;
    public const int MinCopies = 1;
    public const int MaxCopies = 999;

    /// <summary>Decimal places of <see cref="StickerItem.BrickHeight"/> and of the label millimetres.</summary>
    public const int LabelDecimals = 2;

    /// <summary>A4 portrait in millimetres — the sheet the packer uses (<c>PAGE</c> in <c>ui/src/utils/stickerSheets.js</c>).</summary>
    public const decimal SheetPageWidthMm = 210m;
    public const decimal SheetPageHeightMm = 297m;

    /// <summary>The default sheet margin of the SPA (<c>DEFAULT_OPTIONS.marginMm</c>), in millimetres.</summary>
    public const decimal SheetDefaultMarginMm = 8m;

    /// <summary>
    /// Sanity bound of one label: the A4 content area a sheet can print, i.e. the page minus the
    /// default margin on both sides. This is deliberately NOT a re-implementation of the packer —
    /// the client-side skyline raises <c>StickerFitError</c> for a label that does not fit. It only
    /// bounds the one dimension the stud range cannot: a brick stack's height (the tile footprint is
    /// already limited to <see cref="MinStuds"/>..<see cref="MaxStuds"/> studs per side).
    /// </summary>
    public const decimal MaxLabelWidthMm = SheetPageWidthMm - 2 * SheetDefaultMarginMm;
    public const decimal MaxLabelHeightMm = SheetPageHeightMm - 2 * SheetDefaultMarginMm;

    /// <summary>Trims the name and enforces 1..80 characters, or throws a 400-mapped error.</summary>
    public static string NormalizeName(string? name, string entityLabel)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException($"A {entityLabel} needs a name.");
        if (trimmed.Length > MaxNameLength)
            throw new InvalidOperationException(
                $"The {entityLabel} name cannot be longer than {MaxNameLength} characters.");
        return trimmed;
    }

    /// <summary>
    /// Parses the fit name sent by the client ("Contain", "Cover", "Fill", "Center"). Absent or empty
    /// means <see cref="StickerFit.Contain"/>; anything else is a 400-mapped error.
    /// </summary>
    public static StickerFit ParseFit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return StickerFit.Contain;

        var trimmed = value.Trim();
        foreach (var name in Enum.GetNames<StickerFit>())
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<StickerFit>(name);
        }

        throw new InvalidOperationException(
            $"Unknown sticker fit '{value}'. Use Contain, Cover, Fill or Center.");
    }

    /// <summary>
    /// Parses the orientation name sent by the client ("Auto", "Landscape", "Portrait"). Absent or
    /// empty means <see cref="StickerOrientation.Auto"/>; anything else is a 400-mapped error. It is
    /// the item's artwork orientation, not the packer's <c>allowRotate</c>.
    /// </summary>
    public static StickerOrientation ParseOrientation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return StickerOrientation.Auto;

        var trimmed = value.Trim();
        foreach (var name in Enum.GetNames<StickerOrientation>())
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<StickerOrientation>(name);
        }

        throw new InvalidOperationException(
            $"Unknown sticker orientation '{value}'. Use Auto, Landscape or Portrait.");
    }

    /// <summary>
    /// Parses the mount name sent by the client ("Tile", "BrickStack"). Absent or empty means
    /// <see cref="StickerMount.Tile"/> — the same default a missing stored field gets — so a client
    /// that predates the mounts keeps producing tiles; anything else is a 400-mapped error.
    /// </summary>
    public static StickerMount ParseMount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return StickerMount.Tile;

        var trimmed = value.Trim();
        foreach (var name in Enum.GetNames<StickerMount>())
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                return Enum.Parse<StickerMount>(name);
        }

        throw new InvalidOperationException(
            $"Unknown sticker mount '{value}'. Use Tile or BrickStack.");
    }

    /// <summary>
    /// Rounds to the label precision (2 decimals) — used for a brick height and for the label
    /// millimetres, so the number the API reports is the number the sheet prints.
    /// </summary>
    public static decimal RoundLabel(decimal value) =>
        Math.Round(value, LabelDecimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Physical size of one label in millimetres: <c>WidthStuds × 8</c> on the first axis, and on the
    /// second the stud depth of a tile or the brick height of a stack (<c>BrickHeight × 9.6</c>),
    /// rounded to <see cref="LabelDecimals"/> decimals.
    /// </summary>
    public static (decimal WidthMm, decimal DepthMm) LabelSizeMm(StickerItem item)
    {
        var widthMm = item.WidthStuds * (decimal)StickerItem.StudMm;
        var depthMm = item.Mount == StickerMount.BrickStack
            ? RoundLabel((item.BrickHeight ?? 0m) * StickerItem.BrickMm)
            : item.DepthStuds * (decimal)StickerItem.StudMm;
        return (widthMm, depthMm);
    }
}
