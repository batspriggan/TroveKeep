using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// The shared business limits of the sticker feature: one name rule for the image library and the
/// projects, the stud range of an item footprint, the copy range, and the fit/orientation parsers.
/// Kept in one place so the image and project services cannot drift apart.
/// </summary>
public static class StickerRules
{
    public const int MaxNameLength = 80;
    public const int MinStuds = 1;
    public const int MaxStuds = 32;
    public const int MinCopies = 1;
    public const int MaxCopies = 999;

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
}
