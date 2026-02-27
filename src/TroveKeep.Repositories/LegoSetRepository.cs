using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class LegoSetRepository : ILegoSetRepository
{
    private readonly IMongoCollection<LegoSetDocument> _collection;

    public LegoSetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<LegoSetDocument>("legosets");
    }

    public async Task<IEnumerable<LegoSet>> GetAllAsync()
    {
        var docs = await _collection.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<LegoSet?> GetByIdAsync(Guid id)
    {
        var doc = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<LegoSet> CreateAsync(LegoSet legoSet)
    {
        var doc = ToDocument(legoSet);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _collection.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<LegoSet?> UpdateAsync(LegoSet legoSet)
    {
        var existing = await _collection.Find(x => x.Id == legoSet.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(legoSet);
        doc.StorageAllocations = existing.StorageAllocations;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(x => x.Id == legoSet.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<LegoSet?> AddStorageAsync(Guid id, Guid storageId, StorageType type, int quantity)
    {
        var typeStr = type.ToString();
        var options = new FindOneAndUpdateOptions<LegoSetDocument> { ReturnDocument = ReturnDocument.After };

        // Try to increment an existing allocation for this storageId+type
        var matchFilter = Builders<LegoSetDocument>.Filter.And(
            Builders<LegoSetDocument>.Filter.Eq(x => x.Id, id),
            Builders<LegoSetDocument>.Filter.ElemMatch(x => x.StorageAllocations,
                a => a.StorageId == storageId && a.StorageType == typeStr));
        var incUpdate = Builders<LegoSetDocument>.Update
            .Inc("StorageAllocations.$.Quantity", quantity)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var doc = await _collection.FindOneAndUpdateAsync(matchFilter, incUpdate, options);
        if (doc is not null) return ToModel(doc);

        // No existing allocation — push a new entry
        var newAlloc = new StorageAllocationDocument { StorageId = storageId, StorageType = typeStr, Quantity = quantity };
        var pushUpdate = Builders<LegoSetDocument>.Update
            .Push(x => x.StorageAllocations, newAlloc)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, pushUpdate, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<LegoSet?> RemoveStorageAsync(Guid id, Guid storageId)
    {
        var update = Builders<LegoSetDocument>.Update
            .PullFilter(x => x.StorageAllocations, a => a.StorageId == storageId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<LegoSetDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<LegoSet?> ClearStorageAsync(Guid id)
    {
        var update = Builders<LegoSetDocument>.Update
            .Set(x => x.StorageAllocations, new List<StorageAllocationDocument>())
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<LegoSetDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<IEnumerable<LegoSet>> SearchAsync(string query)
    {
        var regex = new BsonRegularExpression(query, "i");
        var filter = Builders<LegoSetDocument>.Filter.Or(
            Builders<LegoSetDocument>.Filter.Regex(d => d.SetNumber, regex),
            Builders<LegoSetDocument>.Filter.Regex(d => d.Description, regex));
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    private static LegoSet ToModel(LegoSetDocument doc) => new()
    {
        Id = doc.Id,
        SetNumber = doc.SetNumber,
        Description = doc.Description,
        PhotoUrl = doc.PhotoUrl,
        Quantity = doc.Quantity,
        StorageAllocations = doc.StorageAllocations.Select(a => new StorageAllocation
        {
            StorageId = a.StorageId,
            Type = Enum.Parse<StorageType>(a.StorageType),
            Quantity = a.Quantity,
        }).ToList(),
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static LegoSetDocument ToDocument(LegoSet model) => new()
    {
        Id = model.Id,
        SetNumber = model.SetNumber,
        Description = model.Description,
        PhotoUrl = model.PhotoUrl,
        Quantity = model.Quantity,
        StorageAllocations = model.StorageAllocations.Select(a => new StorageAllocationDocument
        {
            StorageId = a.StorageId,
            StorageType = a.Type.ToString(),
            Quantity = a.Quantity,
        }).ToList(),
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
