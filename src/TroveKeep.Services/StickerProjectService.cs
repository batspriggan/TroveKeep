using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Business rules of the global sticker projects: a name (trimmed, 1..80 characters) and an item
/// list whose every entry must point at existing artwork from the global image library.
///
/// <para>
/// An item is validated on its own (image exists, footprint 1..32 studs per side, 1..999 copies) and
/// against its siblings: two items with the same <c>ImageId + WidthStuds + DepthStuds</c> are
/// indistinguishable on the sheet, so a second one is refused instead of silently doubling the copies.
/// </para>
///
/// <para>
/// The list may be empty — a project is created first and filled later. An update replaces the item
/// list wholesale but preserves the item ids the client supplied, so a half-typed row survives a
/// reload; items without an id get a fresh Guid.
/// </para>
/// </summary>
public class StickerProjectService : IStickerProjectService
{
    private readonly IStickerProjectRepository _repo;
    private readonly IStickerImageRepository _images;

    public StickerProjectService(IStickerProjectRepository repo, IStickerImageRepository images)
    {
        _repo = repo;
        _images = images;
    }

    public Task<IEnumerable<StickerProject>> GetAllAsync() => _repo.GetAllAsync();

    public Task<StickerProject?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public async Task<StickerProject> CreateAsync(StickerProject project)
    {
        project.Name = StickerRules.NormalizeName(project.Name, "sticker project");
        project.Items = await NormalizeItemsAsync(project.Items);
        return await _repo.CreateAsync(project);
    }

    public async Task<StickerProject> UpdateAsync(Guid id, StickerProject project)
    {
        project.Name = StickerRules.NormalizeName(project.Name, "sticker project");
        project.Items = await NormalizeItemsAsync(project.Items);
        project.Id = id;
        var updated = await _repo.UpdateAsync(project);
        if (updated is null) throw new KeyNotFoundException($"Sticker project {id} not found.");
        return updated;
    }

    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);

    /// <summary>
    /// Validates every item, checks that its artwork exists and that the footprint is unique inside
    /// the project, then returns the list ready to persist (fresh ids where the client sent none).
    /// </summary>
    private async Task<List<StickerItem>> NormalizeItemsAsync(List<StickerItem>? items)
    {
        var normalized = new List<StickerItem>();
        if (items is null) return normalized;

        var seen = new HashSet<(Guid ImageId, int WidthStuds, int DepthStuds)>();
        var knownImages = new HashSet<Guid>();

        foreach (var item in items)
        {
            if (item is null)
                throw new InvalidOperationException("A sticker project item cannot be empty.");

            if (item.ImageId == Guid.Empty)
                throw new InvalidOperationException("A sticker project item needs an image id.");

            // One lookup per referenced image, not one per item.
            if (knownImages.Add(item.ImageId) && await _images.GetByIdAsync(item.ImageId) is null)
                throw new InvalidOperationException(
                    $"Sticker image {item.ImageId} does not exist.");

            if (!Enum.IsDefined(item.Fit))
                throw new InvalidOperationException(
                    $"Unknown sticker fit '{(int)item.Fit}'. Use Contain, Cover, Fill or Center.");

            if (!Enum.IsDefined(item.Orientation))
                throw new InvalidOperationException(
                    $"Unknown sticker orientation '{(int)item.Orientation}'. Use Auto, Landscape or Portrait.");

            if (item.WidthStuds < StickerRules.MinStuds || item.WidthStuds > StickerRules.MaxStuds)
                throw new InvalidOperationException(
                    $"The sticker width must be between {StickerRules.MinStuds} and {StickerRules.MaxStuds} studs.");
            if (item.DepthStuds < StickerRules.MinStuds || item.DepthStuds > StickerRules.MaxStuds)
                throw new InvalidOperationException(
                    $"The sticker depth must be between {StickerRules.MinStuds} and {StickerRules.MaxStuds} studs.");

            if (item.Copies < StickerRules.MinCopies || item.Copies > StickerRules.MaxCopies)
                throw new InvalidOperationException(
                    $"The number of copies must be between {StickerRules.MinCopies} and {StickerRules.MaxCopies}.");

            if (!seen.Add((item.ImageId, item.WidthStuds, item.DepthStuds)))
                throw new InvalidOperationException(
                    $"The project already has an item for image {item.ImageId} " +
                    $"with a {item.WidthStuds}x{item.DepthStuds} stud footprint.");

            normalized.Add(new StickerItem
            {
                Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                ImageId = item.ImageId,
                WidthStuds = item.WidthStuds,
                DepthStuds = item.DepthStuds,
                Copies = item.Copies,
                // Fit is orthogonal metadata with a safe default: never a reason to refuse an item.
                Fit = item.Fit,
                // Same for the artwork orientation: it turns the image, never the label.
                Orientation = item.Orientation,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
            });
        }

        return normalized;
    }
}
