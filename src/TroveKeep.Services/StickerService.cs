using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Business rules of the global sticker library: a name (trimmed, 1..80 characters) and a tile
/// footprint in studs (1..32 per side). The artwork itself is stored through
/// <see cref="IImageRepository"/> as an <see cref="ImageReferenceType.Sticker"/> image keyed by the
/// sticker id, so deleting a sticker also removes its artwork (best effort: an orphan image is
/// harmless and must not fail the delete the user asked for).
/// </summary>
public class StickerService : IStickerService
{
    public const int MaxNameLength = 80;
    public const int MaxStuds = 32;

    private readonly IStickerRepository _repo;
    private readonly IImageRepository _images;

    public StickerService(IStickerRepository repo, IImageRepository images)
    {
        _repo = repo;
        _images = images;
    }

    public Task<IEnumerable<Sticker>> GetAllAsync() => _repo.GetAllAsync();

    public Task<Sticker?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public async Task<Sticker> CreateAsync(Sticker sticker)
    {
        Normalize(sticker);
        sticker.ImageCached = false;
        return await _repo.CreateAsync(sticker);
    }

    public async Task<Sticker> UpdateAsync(Guid id, Sticker sticker)
    {
        Normalize(sticker);
        sticker.Id = id;
        var updated = await _repo.UpdateAsync(sticker);
        if (updated is null) throw new KeyNotFoundException($"Sticker {id} not found.");
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var deleted = await _repo.DeleteAsync(id);
        if (!deleted) return false;

        try
        {
            await _images.DeleteAsync(id.ToString(), ImageReferenceType.Sticker);
        }
        catch
        {
            // Orphaned artwork is harmless: the row is gone, so nothing can reference it.
        }

        return true;
    }

    public async Task<Sticker> UpdateImageCachedAsync(Guid id, bool cached)
    {
        var updated = await _repo.UpdateImageCachedAsync(id, cached);
        if (updated is null) throw new KeyNotFoundException($"Sticker {id} not found.");
        return updated;
    }

    private static void Normalize(Sticker sticker)
    {
        var name = (sticker.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("A sticker needs a name.");
        if (name.Length > MaxNameLength)
            throw new InvalidOperationException($"The sticker name cannot be longer than {MaxNameLength} characters.");

        if (sticker.WidthStuds < 1 || sticker.WidthStuds > MaxStuds)
            throw new InvalidOperationException($"The sticker width must be between 1 and {MaxStuds} studs.");
        if (sticker.DepthStuds < 1 || sticker.DepthStuds > MaxStuds)
            throw new InvalidOperationException($"The sticker depth must be between 1 and {MaxStuds} studs.");

        sticker.Name = name;
    }
}
