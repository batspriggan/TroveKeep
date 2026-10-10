using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class CollectionService : ICollectionService
{
    private readonly ICollectionRepository _collections;
    private readonly ICollectionMemberRepository _members;

    public CollectionService(ICollectionRepository collections, ICollectionMemberRepository members)
    {
        _collections = collections;
        _members = members;
    }

    public Task<IEnumerable<Collection>> GetAllAsync() => _collections.GetAllAsync();

    public Task<Collection?> GetByIdAsync(Guid id) => _collections.GetByIdAsync(id);

    public async Task<IEnumerable<Collection>> GetForUserAsync(Guid userId, bool isAdmin)
    {
        var all = (await _collections.GetAllAsync()).ToList();
        if (isAdmin) return all;

        var ids = await _members.GetCollectionIdsForUserAsync(userId);
        var set = ids.ToHashSet();
        return all.Where(c => set.Contains(c.Id));
    }

    public Task<Collection> CreateAsync(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Collection name is required.");

        return _collections.CreateAsync(new Collection
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        });
    }

    public async Task<Collection?> UpdateAsync(Guid id, string name, string? description)
    {
        var collection = await _collections.GetByIdAsync(id);
        if (collection is null) return null;

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Collection name is required.");

        collection.Name = name.Trim();
        collection.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return await _collections.UpdateAsync(collection);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Members are meaningless without the collection. Domain data is NOT deleted here:
        // that is a destructive operation the admin performs explicitly (not yet exposed).
        await _members.RemoveAllByCollectionAsync(id);
        return await _collections.DeleteAsync(id);
    }

    public Task<IEnumerable<CollectionMember>> GetMembersAsync(Guid collectionId) =>
        _members.GetByCollectionIdAsync(collectionId);

    public Task<CollectionMember> SetMemberAsync(Guid collectionId, Guid userId, CollectionAccess access) =>
        _members.AddOrUpdateAsync(new CollectionMember
        {
            CollectionId = collectionId,
            UserId = userId,
            Access = access,
        });

    public Task<bool> RemoveMemberAsync(Guid collectionId, Guid userId) =>
        _members.RemoveAsync(collectionId, userId);
}
