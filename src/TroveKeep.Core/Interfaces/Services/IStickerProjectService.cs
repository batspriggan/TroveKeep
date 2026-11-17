using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IStickerProjectService
{
    Task<IEnumerable<StickerProject>> GetAllAsync();
    Task<StickerProject?> GetByIdAsync(Guid id);
    Task<StickerProject> CreateAsync(StickerProject project);
    Task<StickerProject> UpdateAsync(Guid id, StickerProject project);
    Task<bool> DeleteAsync(Guid id);
}
