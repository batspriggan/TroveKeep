using System.Globalization;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Business rules of the global sticker projects: a name (trimmed, 1..80 characters) and an item
/// list whose every entry must point at existing artwork from the global image library.
///
/// <para>
/// An item is validated on its own (image exists, width 1..32 studs, 1..999 copies, a stack with a
/// positive brick height) and against its siblings: two items with the same
/// <c>ImageId + Mount + WidthStuds</c> plus the dimension that identifies the label — the depth in
/// studs of a tile, the height in bricks of a stack — are indistinguishable on the sheet, so a second
/// one is refused instead of silently doubling the copies. A tile and a brick stack of the same
/// artwork and size are therefore allowed side by side, as they are different labels.
/// </para>
///
/// <para>
/// The mount also normalises, it never errors: a <see cref="StickerMount.BrickStack"/> ignores the
/// supplied depth and is forced to <c>DepthStuds = 1</c> (only the facade is printed), and a
/// <see cref="StickerMount.Tile"/> drops any brick height.
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
    /// Validates every item, checks that its artwork exists and that the label (mount + size) is
    /// unique inside the project, then returns the list ready to persist (fresh ids where the
    /// client sent none).
    /// </summary>
    private async Task<List<StickerItem>> NormalizeItemsAsync(List<StickerItem>? items)
    {
        var normalized = new List<StickerItem>();
        if (items is null) return normalized;

        // The mount plus the dimension that identifies the label: a tile and a brick stack of the
        // same artwork and size are two different labels, so both slots are part of the key.
        var seen = new HashSet<(Guid ImageId, StickerMount Mount, int WidthStuds, decimal Dimension)>();
        var knownImages = new Dictionary<Guid, StickerImage?>();

        foreach (var item in items)
        {
            if (item is null)
                throw new InvalidOperationException("A sticker project item cannot be empty.");

            if (item.ImageId == Guid.Empty)
                throw new InvalidOperationException("A sticker project item needs an image id.");

            // One lookup per referenced image, not one per item.
            if (!knownImages.TryGetValue(item.ImageId, out var image))
            {
                image = await _images.GetByIdAsync(item.ImageId);
                knownImages[item.ImageId] = image;
            }
            if (image is null)
                throw new InvalidOperationException(
                    $"Sticker image {item.ImageId} does not exist.");

            if (!Enum.IsDefined(item.Mount))
                throw new InvalidOperationException(
                    $"Unknown sticker mount '{(int)item.Mount}'. Use Tile or BrickStack.");

            if (!Enum.IsDefined(item.Fit))
                throw new InvalidOperationException(
                    $"Unknown sticker fit '{(int)item.Fit}'. Use Contain, Cover, Fill or Center.");

            if (!Enum.IsDefined(item.Orientation))
                throw new InvalidOperationException(
                    $"Unknown sticker orientation '{(int)item.Orientation}'. Use Auto, Landscape or Portrait.");

            if (item.WidthStuds < StickerRules.MinStuds || item.WidthStuds > StickerRules.MaxStuds)
                throw new InvalidOperationException(
                    $"The sticker width must be between {StickerRules.MinStuds} and {StickerRules.MaxStuds} studs.");

            var isBrickStack = item.Mount == StickerMount.BrickStack;

            // The mount normalises the second dimension instead of validating it: a stack is one
            // stud deep no matter what the client sent, and a tile has no brick height at all.
            var depthStuds = item.DepthStuds;
            decimal? brickHeight = null;
            if (isBrickStack)
            {
                depthStuds = StickerRules.MinStuds;
                brickHeight = StickerRules.RoundLabel(item.BrickHeight ?? 0m);
                if (brickHeight <= 0m)
                    throw new InvalidOperationException(
                        "A brick stack sticker needs a brick height greater than 0 " +
                        "(the height in bricks, e.g. 6 or 2.33).");
            }
            else if (item.DepthStuds < StickerRules.MinStuds || item.DepthStuds > StickerRules.MaxStuds)
            {
                throw new InvalidOperationException(
                    $"The sticker depth must be between {StickerRules.MinStuds} and {StickerRules.MaxStuds} studs.");
            }

            if (item.Copies < StickerRules.MinCopies || item.Copies > StickerRules.MaxCopies)
                throw new InvalidOperationException(
                    $"The number of copies must be between {StickerRules.MinCopies} and {StickerRules.MaxCopies}.");

            var normalizedItem = new StickerItem
            {
                Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                ImageId = item.ImageId,
                Mount = item.Mount,
                WidthStuds = item.WidthStuds,
                DepthStuds = depthStuds,
                BrickHeight = brickHeight,
                Copies = item.Copies,
                // Fit is orthogonal metadata with a safe default: never a reason to refuse an item.
                Fit = item.Fit,
                // Same for the artwork orientation: it turns the image, never the label.
                Orientation = item.Orientation,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(),
            };

            // Sanity bound, brick stacks only: the height is the one label dimension the stud range
            // cannot bound (a tile is already 1..32 studs per side) and a label taller than the A4
            // content area can never be printed, with or without the packer's 90° fallback. This is
            // not the packer: an over-wide label is caught by the client-side skyline.
            if (isBrickStack)
            {
                var (widthMm, heightMm) = StickerRules.LabelSizeMm(normalizedItem);
                if (heightMm > StickerRules.MaxLabelHeightMm)
                    throw new InvalidOperationException(
                        $"The sticker label for image '{image.Name}' would be " +
                        $"{widthMm.ToString("0.##", CultureInfo.InvariantCulture)} × " +
                        $"{heightMm.ToString("0.##", CultureInfo.InvariantCulture)} mm, larger than the printable " +
                        $"A4 area ({StickerRules.MaxLabelWidthMm} × {StickerRules.MaxLabelHeightMm} mm) — " +
                        "reduce the brick height.");
            }

            var dimension = isBrickStack ? brickHeight!.Value : depthStuds;
            if (!seen.Add((item.ImageId, item.Mount, item.WidthStuds, dimension)))
                throw new InvalidOperationException(
                    isBrickStack
                        ? $"The project already has an item for image {item.ImageId} with a " +
                          $"{item.WidthStuds} stud facade on {brickHeight!.Value} bricks."
                        : $"The project already has an item for image {item.ImageId} " +
                          $"with a {item.WidthStuds}x{item.DepthStuds} stud footprint.");

            normalized.Add(normalizedItem);
        }

        return normalized;
    }
}
