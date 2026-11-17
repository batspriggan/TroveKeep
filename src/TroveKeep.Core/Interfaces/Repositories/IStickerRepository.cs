using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

/// <summary>
/// Storage for the global sticker library. Deliberately <em>not</em> collection-scoped: there is
/// no <c>CollectionId</c> on a sticker and therefore no scoped filter — every authenticated user
/// reads the same library (see <see cref="Sticker"/> and <c>CollectionWriteAnywhereFilter</c> for
/// the write rule).
/// </summary>
public interface IStickerRepository
{
    Task<IEnumerable<Sticker>> GetAllAsync();
    Task<Sticker?> GetByIdAsync(Guid id);
    Task<Sticker> CreateAsync(Sticker sticker);
    Task<Sticker?> UpdateAsync(Sticker sticker);
    Task<bool> DeleteAsync(Guid id);

    /// <summary>Sets <see cref="Sticker.ImageCached"/>, bumping the version so the card reloads.</summary>
    Task<Sticker?> UpdateImageCachedAsync(Guid id, bool cached);
}
