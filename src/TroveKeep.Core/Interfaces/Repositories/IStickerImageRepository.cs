using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

/// <summary>
/// Storage for the global sticker image library. Deliberately <em>not</em> collection-scoped: there
/// is no <c>CollectionId</c> on an image and therefore no scoped filter — every authenticated user
/// reads the same library (see <see cref="StickerImage"/> and <c>CollectionWriteAnywhereFilter</c>
/// for the write rule).
/// </summary>
public interface IStickerImageRepository
{
    Task<IEnumerable<StickerImage>> GetAllAsync();
    Task<StickerImage?> GetByIdAsync(Guid id);
    Task<StickerImage> CreateAsync(StickerImage image);
    Task<StickerImage?> UpdateAsync(StickerImage image);
    Task<bool> DeleteAsync(Guid id);

    /// <summary>Sets <see cref="StickerImage.ImageCached"/>, bumping the version so the card reloads.</summary>
    Task<StickerImage?> UpdateImageCachedAsync(Guid id, bool cached);
}
