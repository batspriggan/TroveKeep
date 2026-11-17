using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IStickerImageService
{
    Task<IEnumerable<StickerImage>> GetAllAsync();
    Task<StickerImage?> GetByIdAsync(Guid id);
    Task<StickerImage> CreateAsync(StickerImage image);
    Task<StickerImage> UpdateAsync(Guid id, StickerImage image);
    Task<bool> DeleteAsync(Guid id);
    Task<StickerImage> UpdateImageCachedAsync(Guid id, bool cached);
}
