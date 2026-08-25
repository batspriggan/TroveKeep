using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class LabelTargetRepository : ILabelTargetRepository
{
    private readonly IMongoCollection<LabelTargetDocument> _collection;

    public LabelTargetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<LabelTargetDocument>("label_targets");
    }

    public async Task<LabelTarget?> GetByKeyAsync(string key)
    {
        var doc = await _collection.Find(x => x.Key == key).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<IEnumerable<LabelTarget>> GetByStorageAsync(Guid storageId, int? position = null)
    {
        var filter = Builders<LabelTargetDocument>.Filter.Eq(x => x.StorageId, storageId);
        if (position.HasValue)
            filter &= Builders<LabelTargetDocument>.Filter.Eq(x => x.StoragePosition, position.Value);
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<LabelTarget> UpsertAsync(LabelTarget target)
    {
        var now = DateTime.UtcNow;
        var doc = new LabelTargetDocument
        {
            Key = target.Key,
            TargetType = target.TargetType.ToString(),
            StorageId = target.StorageId,
            StoragePosition = target.StoragePosition,
            CreatedAt = target.CreatedAt.UtcDateTime == default ? now : target.CreatedAt.UtcDateTime,
            UpdatedAt = now,
        };
        await _collection.ReplaceOneAsync(
            Builders<LabelTargetDocument>.Filter.Eq(x => x.Key, target.Key),
            doc,
            new ReplaceOptions { IsUpsert = true });
        return ToModel(doc);
    }

    public async Task DeleteByStorageAsync(Guid storageId, int? position = null)
    {
        var filter = Builders<LabelTargetDocument>.Filter.Eq(x => x.StorageId, storageId);
        if (position.HasValue)
            filter &= Builders<LabelTargetDocument>.Filter.Eq(x => x.StoragePosition, position.Value);
        await _collection.DeleteManyAsync(filter);
    }

    public async Task UpdateStorageAsync(Guid srcStorageId, int? srcPosition, Guid dstStorageId, int? dstPosition)
    {
        var filter = Builders<LabelTargetDocument>.Filter.And(
            Builders<LabelTargetDocument>.Filter.Eq(x => x.StorageId, srcStorageId),
            Builders<LabelTargetDocument>.Filter.Eq(x => x.StoragePosition, srcPosition));
        var update = Builders<LabelTargetDocument>.Update
            .Set(x => x.StorageId, dstStorageId)
            .Set(x => x.StoragePosition, dstPosition)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateManyAsync(filter, update);
    }

    private static LabelTarget ToModel(LabelTargetDocument doc) => new()
    {
        Key = doc.Key,
        TargetType = Enum.Parse<StorageType>(doc.TargetType),
        StorageId = doc.StorageId,
        StoragePosition = doc.StoragePosition,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };
}
