using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ICollectionService
{
    Task<IEnumerable<Collection>> GetAllAsync();
    Task<Collection?> GetByIdAsync(Guid id);

    /// <summary>Collections the given user belongs to (all of them for an admin).</summary>
    Task<IEnumerable<Collection>> GetForUserAsync(Guid userId, bool isAdmin);

    Task<Collection> CreateAsync(string name, string? description);
    Task<Collection?> UpdateAsync(Guid id, string name, string? description);
    Task<bool> DeleteAsync(Guid id);

    Task<IEnumerable<CollectionMember>> GetMembersAsync(Guid collectionId);

    /// <summary>Adds a member or updates the access level of an existing one.</summary>
    Task<CollectionMember> SetMemberAsync(Guid collectionId, Guid userId, CollectionAccess access);

    Task<bool> RemoveMemberAsync(Guid collectionId, Guid userId);
}
