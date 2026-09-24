using System.Security.Cryptography;
using System.Text;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Dissolves legacy MOC rows into real baseplate placements. See
/// <see cref="IMocReconciliationService"/> for the contract.
///
/// The service only depends on repository interfaces (never on <c>IServiceProvider</c>), so
/// <c>Migration_005</c> can build it by hand from an <c>IMongoDatabase</c> while DI wires the
/// same instance graph for the API.
/// </summary>
public class MocReconciliationService : IMocReconciliationService
{
    /// <summary>Nominal millimetres per stud (matches the planner canvas convention).</summary>
    private const int MmPerStud = 8;

    private readonly IBaseplateRepository _baseplateRepo;
    private readonly IRoomRepository _roomRepo;
    private readonly IImageRepository _imageRepo;

    public MocReconciliationService(
        IBaseplateRepository baseplateRepo,
        IRoomRepository roomRepo,
        IImageRepository imageRepo)
    {
        _baseplateRepo = baseplateRepo;
        _roomRepo = roomRepo;
        _imageRepo = imageRepo;
    }

    public async Task<Baseplate?> FindModuleAsync(int widthStuds, int depthStuds, int legoColorId, Guid excludeId)
    {
        // The plate a MOC is built on is a standard baseplate: prefer Standard, then Road, then
        // Custom. The colour criterion only applies when a colour is actually set (0 = "no colour",
        // which every non-Standard plate carries — matching on 0 made roads win over standard
        // plates, since a colourless road and a colourless MOC both have LegoColorId == 0).
        var hasColor = legoColorId > 0;

        var candidates = (await _baseplateRepo.GetAllAsync())
            .Where(bp => bp.Id != excludeId)
            .Where(IsRealPlate)
            .Where(bp => bp.WidthStuds > 0 && bp.DepthStuds > 0)
            .Where(bp => Divides(widthStuds, depthStuds, bp.WidthStuds, bp.DepthStuds))
            .OrderByDescending(bp => bp.Type == BaseplateType.Standard)
            .ThenByDescending(bp => bp.Type == BaseplateType.Road ? 1 : 0)
            .ThenByDescending(bp => hasColor && bp.LegoColorId == legoColorId)
            .ThenByDescending(bp => (long)bp.WidthStuds * bp.DepthStuds)
            .ThenBy(bp => bp.Id)
            .ToList();

        return candidates.FirstOrDefault();
    }

    public async Task<MocDissolveResult> DissolveAsync(Guid mocBaseplateId, Guid targetBaseplateId, int cols, int rows)
    {
        if (cols < 1 || rows < 1)
            return new MocDissolveResult(false, 0, 0, "invalid arrangement");

        var moc = await _baseplateRepo.GetByIdAsync(mocBaseplateId);
        if (moc is null)
            return new MocDissolveResult(false, 0, 0, "not found");

        if (moc.LinkedSetId is null)
        {
            await QuarantineAsync(mocBaseplateId, "linked set not found");
            return new MocDissolveResult(false, 0, 0, "linked set not found");
        }

        var target = await _baseplateRepo.GetByIdAsync(targetBaseplateId);
        if (target is null)
            return new MocDissolveResult(false, 0, 0, "target baseplate not found");

        var sourceSetId = moc.LinkedSetId.Value;
        var totalPlates = cols * rows;

        // 1. Reserve the plates the MOC occupies. An over-reservation (more plates claimed than
        //    owned) is deliberately allowed: it is a warning shown in the library, not an error,
        //    and the user reconciles the owned quantity there.
        var reserved = await _baseplateRepo.AddOrIncrementReservationAsync(targetBaseplateId, sourceSetId, totalPlates);
        if (reserved is null)
            return new MocDissolveResult(false, 0, 0, "target baseplate not found");

        // 2. Rewrite every placement of the MOC row into a cols×rows grid of real plates.
        var placementsRewritten = 0;
        foreach (var room in await _roomRepo.GetAllAsync())
        {
            var roomChanged = false;

            foreach (var layout in room.AggregateBpLayouts)
            {
                if (!layout.PlacedBaseplates.Any(p => p.BaseplateId == mocBaseplateId))
                    continue;

                var rewritten = new List<PlacedBaseplate>(layout.PlacedBaseplates.Count + totalPlates);

                foreach (var placed in layout.PlacedBaseplates)
                {
                    if (placed.BaseplateId != mocBaseplateId)
                    {
                        rewritten.Add(placed);
                        continue;
                    }

                    placementsRewritten++;
                    rewritten.AddRange(BuildGrid(placed, target, sourceSetId, cols, rows));
                }

                layout.PlacedBaseplates = rewritten;
                roomChanged = true;
            }

            if (roomChanged)
                await _roomRepo.SaveAggregateBpLayoutsAsync(room.Id, room.AggregateBpLayouts);
        }

        // 3. Delete the MOC row, then its cached image (best-effort: a missing image must not
        //    make the reconciliation fail after the layout was already rewritten).
        await _baseplateRepo.DeleteAsync(mocBaseplateId);
        try
        {
            await _imageRepo.DeleteAsync(mocBaseplateId.ToString(), ImageReferenceType.Baseplate);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[MocReconciliation] WARNING: could not delete the image of baseplate {mocBaseplateId}: {ex.Message}");
        }

        return new MocDissolveResult(true, totalPlates, placementsRewritten, null);
    }

    public async Task QuarantineAsync(Guid baseplateId, string reason)
    {
        var existing = await _baseplateRepo.GetByIdAsync(baseplateId);
        if (existing is null) return;

        existing.Quarantined = true;
        existing.QuarantineReason = reason;
        existing.NeedsReview = true;

        await _baseplateRepo.UpdateAsync(existing);
    }

    /// <summary>A real (non-MOC) catalogue row: <c>Type != Custom</c> or no linked set.</summary>
    private static bool IsRealPlate(Baseplate bp) =>
        bp.Type != BaseplateType.Custom || bp.LinkedSetId is null;

    /// <summary>
    /// True when the module divides the footprint exactly, in either orientation.
    /// </summary>
    private static bool Divides(int widthStuds, int depthStuds, int moduleWidth, int moduleDepth) =>
        (widthStuds % moduleWidth == 0 && depthStuds % moduleDepth == 0) ||
        (widthStuds % moduleDepth == 0 && depthStuds % moduleWidth == 0);

    /// <summary>
    /// Builds the cols×rows grid that replaces a single MOC placement. Steps follow the placement
    /// rotation (a 90°/270° placement swaps the module's footprint) using nominal millimetres.
    /// </summary>
    private static IEnumerable<PlacedBaseplate> BuildGrid(
        PlacedBaseplate source, Baseplate target, Guid sourceSetId, int cols, int rows)
    {
        var rotated = NormalizeRotation(source.Rotation) is 90 or 270;
        var stepX = (rotated ? target.DepthStuds : target.WidthStuds) * MmPerStud;
        var stepY = (rotated ? target.WidthStuds : target.DepthStuds) * MmPerStud;

        for (var iy = 0; iy < rows; iy++)
        {
            for (var ix = 0; ix < cols; ix++)
            {
                yield return new PlacedBaseplate
                {
                    InstanceId = DeterministicInstanceId(source.InstanceId, ix, iy),
                    BaseplateId = target.Id,
                    XMm = source.XMm + ix * stepX,
                    YMm = source.YMm + iy * stepY,
                    Rotation = source.Rotation,
                    SourceSetId = sourceSetId,
                    // One placement instance per dissolved MOC block: the MOC can be placed
                    // again later and the two blocks must stay independent.
                    PlacementId = source.InstanceId,
                };
            }
        }
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = rotation % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    /// <summary>
    /// Stable instance id derived from the original placement and the grid cell, so a partially
    /// applied reconciliation (e.g. a retried migration) produces the same ids instead of littering
    /// the layout with fresh Guids.
    /// </summary>
    private static Guid DeterministicInstanceId(Guid originalInstanceId, int ix, int iy)
    {
        var bytes = Encoding.UTF8.GetBytes($"{originalInstanceId:N}:{ix}:{iy}");
        var hash = SHA256.HashData(bytes);
        return new Guid(hash.AsSpan(0, 16));
    }
}
