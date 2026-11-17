using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IStickerService
{
    Task<IEnumerable<Sticker>> GetAllAsync();
    Task<Sticker?> GetByIdAsync(Guid id);
    Task<Sticker> CreateAsync(Sticker sticker);
    Task<Sticker> UpdateAsync(Guid id, Sticker sticker);
    Task<bool> DeleteAsync(Guid id);
    Task<Sticker> UpdateImageCachedAsync(Guid id, bool cached);
}
