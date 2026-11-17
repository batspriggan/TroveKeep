using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

/// <summary>
/// Storage for the global sticker projects (collection <c>sticker_projects</c>). Like the image
/// library it is not collection-scoped: the items are embedded, so a project is one document that is
/// always read and written as a whole.
/// </summary>
public interface IStickerProjectRepository
{
    Task<IEnumerable<StickerProject>> GetAllAsync();
    Task<StickerProject?> GetByIdAsync(Guid id);
    Task<StickerProject> CreateAsync(StickerProject project);
    Task<StickerProject?> UpdateAsync(StickerProject project);
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// How many project items (across every project) reference <paramref name="imageId"/>. Used to
    /// refuse deleting artwork that is still printed by a project.
    /// </summary>
    Task<int> CountUsagesAsync(Guid imageId);
}
