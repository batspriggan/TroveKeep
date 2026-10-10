using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ICollectionMemberRepository
{
    Task<IEnumerable<CollectionMember>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<CollectionMember>> GetByCollectionIdAsync(Guid collectionId);

    /// <summary>Collection ids the user is a member of. Used to scope every domain query.</summary>
    Task<IReadOnlyCollection<Guid>> GetCollectionIdsForUserAsync(Guid userId);

    Task<CollectionMember?> GetAsync(Guid collectionId, Guid userId);
    Task<CollectionMember> AddOrUpdateAsync(CollectionMember member);
    Task<bool> RemoveAsync(Guid collectionId, Guid userId);
    Task RemoveAllByCollectionAsync(Guid collectionId);
    Task RemoveAllByUserAsync(Guid userId);
}
