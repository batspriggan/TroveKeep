using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class AllocationRepository : IAllocationRepository
{
    private readonly IMongoCollection<StorageAllocationDocument> _collection;

    public AllocationRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<StorageAllocationDocument>("storage_allocations");

        var itemIdIndex = new CreateIndexModel<StorageAllocationDocument>(
            Builders<StorageAllocationDocument>.IndexKeys.Ascending(x => x.ItemId));
        var storageIdIndex = new CreateIndexModel<StorageAllocationDocument>(
            Builders<StorageAllocationDocument>.IndexKeys.Ascending(x => x.StorageId));
        var uniqueIndex = new CreateIndexModel<StorageAllocationDocument>(
            Builders<StorageAllocationDocument>.IndexKeys
                .Ascending(x => x.ItemId)
                .Ascending(x => x.StorageId)
                .Ascending(x => x.StoragePosition),
            new CreateIndexOptions { Unique = true });

        _collection.Indexes.CreateMany([itemIdIndex, storageIdIndex, uniqueIndex]);
    }

    public async Task<IEnumerable<StorageAllocation>> GetByItemAsync(Guid itemId)
    {
        var docs = await _collection.Find(x => x.ItemId == itemId).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<IEnumerable<StorageAllocation>> GetByItemsAsync(IEnumerable<Guid> itemIds)
    {
        var idList = itemIds.ToList();
        if (idList.Count == 0) return [];
        var docs = await _collection.Find(x => idList.Contains(x.ItemId)).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<IEnumerable<StorageAllocation>> GetByStorageAsync(Guid storageId, int? position = null)
    {
        var filter = Builders<StorageAllocationDocument>.Filter.Eq(x => x.StorageId, storageId);
        if (position.HasValue)
            filter &= Builders<StorageAllocationDocument>.Filter.Eq(x => x.StoragePosition, position.Value);
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<IEnumerable<StorageAllocation>> GetByStoragesAsync(IEnumerable<Guid> storageIds)
    {
        var idList = storageIds.ToList();
        if (idList.Count == 0) return [];
        var docs = await _collection.Find(x => idList.Contains(x.StorageId)).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<StorageAllocation> AddOrIncrementAsync(
        Guid itemId, string itemType, Guid storageId, StorageType storageType, int quantity, int? storagePosition = null)
    {
        var filter = Builders<StorageAllocationDocument>.Filter.And(
            Builders<StorageAllocationDocument>.Filter.Eq(x => x.ItemId, itemId),
            Builders<StorageAllocationDocument>.Filter.Eq(x => x.StorageId, storageId),
            Builders<StorageAllocationDocument>.Filter.Eq(x => x.StoragePosition, storagePosition));

        var existing = await _collection.Find(filter).FirstOrDefaultAsync();

        if (existing is not null)
        {
            var update = Builders<StorageAllocationDocument>.Update
                .Inc(x => x.Quantity, quantity)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            var options = new FindOneAndUpdateOptions<StorageAllocationDocument>
            {
                ReturnDocument = ReturnDocument.After
            };
            var updated = await _collection.FindOneAndUpdateAsync(
                x => x.Id == existing.Id, update, options);
            return ToModel(updated);
        }

        var now = DateTime.UtcNow;
        var doc = new StorageAllocationDocument
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            ItemType = itemType,
            StorageId = storageId,
            StoragePosition = storagePosition,
            StorageType = storageType.ToString(),
            Quantity = quantity,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _collection.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<bool> RemoveByItemAndStorageAsync(Guid itemId, Guid storageId, int? storagePosition = null)
    {
        var filter = Builders<StorageAllocationDocument>.Filter.And(
            Builders<StorageAllocationDocument>.Filter.Eq(x => x.ItemId, itemId),
            Builders<StorageAllocationDocument>.Filter.Eq(x => x.StorageId, storageId));
        if (storagePosition.HasValue)
            filter &= Builders<StorageAllocationDocument>.Filter.Eq(x => x.StoragePosition, storagePosition.Value);
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task RemoveAllByItemAsync(Guid itemId)
    {
        await _collection.DeleteManyAsync(x => x.ItemId == itemId);
    }

    public async Task RemoveAllByStorageAsync(Guid storageId, int? position = null)
    {
        if (position.HasValue)
        {
            var filter = Builders<StorageAllocationDocument>.Filter.And(
                Builders<StorageAllocationDocument>.Filter.Eq(x => x.StorageId, storageId),
                Builders<StorageAllocationDocument>.Filter.Eq(x => x.StoragePosition, position.Value));
            await _collection.DeleteManyAsync(filter);
        }
        else
        {
            await _collection.DeleteManyAsync(x => x.StorageId == storageId);
        }
    }

    private static StorageAllocation ToModel(StorageAllocationDocument doc) => new()
    {
        Id = doc.Id,
        ItemId = doc.ItemId,
        ItemType = doc.ItemType,
        StorageId = doc.StorageId,
        StoragePosition = doc.StoragePosition,
        StorageType = Enum.Parse<StorageType>(doc.StorageType),
        Quantity = doc.Quantity,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };
}
