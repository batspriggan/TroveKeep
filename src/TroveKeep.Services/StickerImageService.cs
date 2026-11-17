using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Business rules of the global sticker image library: a name (trimmed, 1..80 characters) and the
/// artwork stored through <see cref="IImageRepository"/> as an
/// <see cref="ImageReferenceType.StickerImage"/> image keyed by the image id.
///
/// <para>
/// An image that a project item still prints must not be deleted (the item would turn into a broken
/// label): the delete is refused with a 400 naming how many items use it. Deleting an unused image
/// also removes its artwork, best effort — an orphan image is harmless and must not fail the delete
/// the user asked for.
/// </para>
/// </summary>
public class StickerImageService : IStickerImageService
{
    private readonly IStickerImageRepository _repo;
    private readonly IStickerProjectRepository _projects;
    private readonly IImageRepository _images;

    public StickerImageService(
        IStickerImageRepository repo,
        IStickerProjectRepository projects,
        IImageRepository images)
    {
        _repo = repo;
        _projects = projects;
        _images = images;
    }

    public Task<IEnumerable<StickerImage>> GetAllAsync() => _repo.GetAllAsync();

    public Task<StickerImage?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public async Task<StickerImage> CreateAsync(StickerImage image)
    {
        image.Name = StickerRules.NormalizeName(image.Name, "sticker image");
        image.ImageCached = false;
        return await _repo.CreateAsync(image);
    }

    public async Task<StickerImage> UpdateAsync(Guid id, StickerImage image)
    {
        image.Name = StickerRules.NormalizeName(image.Name, "sticker image");
        image.Id = id;
        var updated = await _repo.UpdateAsync(image);
        if (updated is null) throw new KeyNotFoundException($"Sticker image {id} not found.");
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var usages = await _projects.CountUsagesAsync(id);
        if (usages > 0)
            throw new InvalidOperationException(
                $"Sticker image {id} is used by {usages} project item(s); remove them first.");

        var deleted = await _repo.DeleteAsync(id);
        if (!deleted) return false;

        try
        {
            await _images.DeleteAsync(id.ToString(), ImageReferenceType.StickerImage);
        }
        catch
        {
            // Orphaned artwork is harmless: the row is gone, so nothing can reference it.
        }

        return true;
    }

    public async Task<StickerImage> UpdateImageCachedAsync(Guid id, bool cached)
    {
        var updated = await _repo.UpdateImageCachedAsync(id, cached);
        if (updated is null) throw new KeyNotFoundException($"Sticker image {id} not found.");
        return updated;
    }
}
