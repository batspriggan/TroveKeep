using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class CollectionMemberRepository : ICollectionMemberRepository
{
    private readonly IMongoCollection<CollectionMemberDocument> _members;

    public CollectionMemberRepository(IMongoDatabase database)
    {
        _members = database.GetCollection<CollectionMemberDocument>("collection_members");
    }

    public async Task<IEnumerable<CollectionMember>> GetByUserIdAsync(Guid userId)
    {
        var docs = await _members.Find(x => x.UserId == userId).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<IEnumerable<CollectionMember>> GetByCollectionIdAsync(Guid collectionId)
    {
        var docs = await _members.Find(x => x.CollectionId == collectionId).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<IReadOnlyCollection<Guid>> GetCollectionIdsForUserAsync(Guid userId)
    {
        var ids = await _members.Find(x => x.UserId == userId)
            .Project(x => x.CollectionId)
            .ToListAsync();
        return ids;
    }

    public async Task<CollectionMember?> GetAsync(Guid collectionId, Guid userId)
    {
        var doc = await _members.Find(x => x.CollectionId == collectionId && x.UserId == userId)
            .FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<CollectionMember> AddOrUpdateAsync(CollectionMember member)
    {
        var existing = await _members
            .Find(x => x.CollectionId == member.CollectionId && x.UserId == member.UserId)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            var update = Builders<CollectionMemberDocument>.Update.Set(x => x.Access, (int)member.Access);
            await _members.UpdateOneAsync(x => x.Id == existing.Id, update);
            existing.Access = (int)member.Access;
            return ToModel(existing);
        }

        var doc = ToDocument(member);
        doc.Id = member.Id == Guid.Empty ? Guid.NewGuid() : member.Id;
        doc.AddedAt = DateTime.UtcNow;
        await _members.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<bool> RemoveAsync(Guid collectionId, Guid userId)
    {
        var result = await _members.DeleteOneAsync(x => x.CollectionId == collectionId && x.UserId == userId);
        return result.DeletedCount > 0;
    }

    public async Task RemoveAllByCollectionAsync(Guid collectionId) =>
        await _members.DeleteManyAsync(x => x.CollectionId == collectionId);

    public async Task RemoveAllByUserAsync(Guid userId) =>
        await _members.DeleteManyAsync(x => x.UserId == userId);

    private static CollectionMember ToModel(CollectionMemberDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        UserId = doc.UserId,
        Access = (CollectionAccess)doc.Access,
        AddedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.AddedAt, DateTimeKind.Utc)),
    };

    private static CollectionMemberDocument ToDocument(CollectionMember model) => new()
    {
        Id = model.Id,
        CollectionId = model.CollectionId,
        UserId = model.UserId,
        Access = (int)model.Access,
        AddedAt = model.AddedAt.UtcDateTime,
    };
}
