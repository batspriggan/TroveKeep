using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Converts legacy MOC rows (<c>Type == Custom &amp;&amp; LinkedSetId != null</c>) into real
/// baseplate placements. Shared by <c>Migration_005</c> and the reconciliation wizard so both
/// paths produce exactly the same result.
/// </summary>
public interface IMocReconciliationService
{
    /// <summary>
    /// Finds the real baseplate type whose dimensions divide the given footprint exactly,
    /// also matching when the module is rotated (width/depth swapped).
    /// Preference: same <paramref name="legoColorId"/>, then <see cref="BaseplateType.Standard"/>,
    /// then larger dimensions. <paramref name="excludeId"/> is never returned.
    /// </summary>
    Task<Baseplate?> FindModuleAsync(int widthStuds, int depthStuds, int legoColorId, Guid excludeId);

    /// <summary>
    /// Dissolves a MOC row: reserves <paramref name="cols"/>×<paramref name="rows"/> plates of the
    /// target type for the linked set, rewrites every matching <c>PlacedBaseplate</c> into a
    /// cols×rows grid carrying <c>SourceSetId</c>, then deletes the MOC row (and its image,
    /// best-effort).
    /// </summary>
    Task<MocDissolveResult> DissolveAsync(Guid mocBaseplateId, Guid targetBaseplateId, int cols, int rows);

    /// <summary>
    /// Marks a row as non-convertible without deleting it: <c>Quarantined=true</c>,
    /// <c>QuarantineReason=reason</c>, <c>NeedsReview=true</c>.
    /// </summary>
    Task QuarantineAsync(Guid baseplateId, string reason);
}

public record MocDissolveResult(bool Dissolved, int PlatesCreated, int PlacementsRewritten, string? Reason);
