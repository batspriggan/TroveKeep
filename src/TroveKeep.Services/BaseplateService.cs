using System.Text.RegularExpressions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BaseplateService : IBaseplateService
{
    private readonly IBaseplateRepository _repo;
    private readonly IRoomRepository _roomRepo;

    public BaseplateService(IBaseplateRepository repo, IRoomRepository roomRepo)
    {
        _repo = repo;
        _roomRepo = roomRepo;
    }

    public Task<IEnumerable<Baseplate>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Baseplate?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public Task<Baseplate> CreateAsync(Baseplate baseplate) => CreateAsync(baseplate, isImported: false);

    public Task<Baseplate> CreateAsync(Baseplate baseplate, bool isImported)
    {
        Normalize(baseplate);
        baseplate.NeedsReview = ComputeNeedsReview(baseplate, isImported);
        return _repo.CreateAsync(baseplate);
    }

    public Task UpdateImageCachedAsync(Guid id, bool cached) => _repo.UpdateImageCachedAsync(id, cached);

    public async Task DeleteAsync(Guid id)
    {
        // Bonify room layouts first so a successful delete never leaves a dangling
        // PlacedBaseplate reference behind.
        await _roomRepo.RemoveBaseplateReferencesAsync(id);
        await _repo.DeleteAsync(id);
    }

    public async Task<Baseplate> UpdateAsync(Guid id, Baseplate updated)
    {
        var existing = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");

        Normalize(updated);
        updated.Id = id;
        updated.CreatedAt = existing.CreatedAt;
        updated.ImageCached = existing.ImageCached;
        updated.LinkedSetId = updated.LinkedSetId ?? existing.LinkedSetId;
        // Reservations are managed only via the dedicated endpoints.
        updated.Reservations = existing.Reservations;
        // Quarantine is resolved through the dedicated endpoints, never by a normal edit.
        updated.Quarantined = existing.Quarantined;
        updated.QuarantineReason = existing.QuarantineReason;
        updated.NeedsReview = ComputeNeedsReview(updated, isImported: false);

        var saved = await _repo.UpdateAsync(updated)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");
        return saved;
    }

    public async Task<Baseplate> AddReservationAsync(Guid id, Guid setId, int quantity)
    {
        if (quantity < 1)
            throw new InvalidOperationException("Reservation quantity must be at least 1.");

        var updated = await _repo.AddOrIncrementReservationAsync(id, setId, quantity)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");
        return updated;
    }

    public async Task<Baseplate> RemoveReservationAsync(Guid id, Guid setId)
    {
        _ = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");

        await _repo.RemoveReservationAsync(id, setId);
        return await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");
    }

    public Task RemoveReservationsBySetIdAsync(Guid setId) => _repo.RemoveReservationsBySetIdAsync(setId);

    public async Task<Baseplate> ConfirmAsync(Guid id)
    {
        var existing = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");

        existing.NeedsReview = false;
        return await _repo.UpdateAsync(existing)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");
    }

    public async Task<Baseplate> UnquarantineAsync(Guid id, bool confirmAsPhysicalPlate)
    {
        var existing = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");

        existing.Quarantined = false;
        existing.QuarantineReason = null;
        existing.NeedsReview = false;
        // "Not a MOC": the row becomes a real physical plate and is no longer bound to a set.
        if (confirmAsPhysicalPlate) existing.LinkedSetId = null;

        return await _repo.UpdateAsync(existing)
            ?? throw new KeyNotFoundException($"Baseplate {id} not found.");
    }

    public bool ComputeNeedsReview(Baseplate bp, bool isImported)
    {
        // Imported rows (legacy backlog / migration dedup) always need a human check first.
        // This flag is NOT set by the UI: a row created from the part archive carries guessed
        // dimensions too, but those are visible in the form the user just filled in.
        if (isImported) return true;

        if (bp.Quantity < 1) return true;

        return bp.Type switch
        {
            BaseplateType.Standard =>
                bp.WidthStuds <= 0 || bp.DepthStuds <= 0 || bp.LegoColorId <= 0,
            // The road shape is optional metadata, not a completeness requirement: legacy road
            // plates carry it as null, and demanding it would re-flag them on every edit (even a
            // quantity bump), making the review flag impossible to clear.
            BaseplateType.Road =>
                bp.WidthStuds <= 0 || bp.DepthStuds <= 0,
            BaseplateType.Custom =>
                bp.WidthStuds <= 0 || bp.DepthStuds <= 0 || string.IsNullOrWhiteSpace(bp.Name),
            _ => true,
        };
    }

    private static void Normalize(Baseplate bp)
    {
        bp.Reservations ??= [];
        // RoadShape is only meaningful for Road baseplates.
        if (bp.Type != BaseplateType.Road) bp.RoadShape = null;
    }

    // Matches patterns like "48 x 48", "16 x 32", "24X32", "48×48"
    private static readonly Regex _studPattern =
        new(@"(\d+)\s*[xX×]\s*(\d+)", RegexOptions.Compiled);

    public (int studX, int studY) GuessStudDimensions(string partDescription)
    {
        var match = _studPattern.Match(partDescription);
        if (!match.Success) return (0, 0);
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }
}
