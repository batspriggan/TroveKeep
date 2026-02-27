using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BulkPieceRepository : IBulkPieceRepository
{
    private readonly IMongoCollection<BulkPieceDocument> _collection;

    public BulkPieceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<BulkPieceDocument>("bulkpieces");
    }

    public async Task<IEnumerable<BulkPiece>> GetAllAsync()
    {
        var docs = await _collection.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<BulkPiece?> GetByIdAsync(Guid id)
    {
        var doc = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece> CreateAsync(BulkPiece bulkPiece)
    {
        var doc = ToDocument(bulkPiece);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _collection.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece)
    {
        var existing = await _collection.Find(x => x.Id == bulkPiece.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(bulkPiece);
        doc.StorageAllocations = existing.StorageAllocations;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(x => x.Id == bulkPiece.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<BulkPiece?> AddStorageAsync(Guid id, Guid storageId, StorageType type, int quantity)
    {
        var typeStr = type.ToString();
        var options = new FindOneAndUpdateOptions<BulkPieceDocument> { ReturnDocument = ReturnDocument.After };

        // Try to increment an existing allocation for this storageId+type
        var matchFilter = Builders<BulkPieceDocument>.Filter.And(
            Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, id),
            Builders<BulkPieceDocument>.Filter.ElemMatch(x => x.StorageAllocations,
                a => a.StorageId == storageId && a.StorageType == typeStr));
        var incUpdate = Builders<BulkPieceDocument>.Update
            .Inc("StorageAllocations.$.Quantity", quantity)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var doc = await _collection.FindOneAndUpdateAsync(matchFilter, incUpdate, options);
        if (doc is not null) return ToModel(doc);

        // No existing allocation — push a new entry
        var newAlloc = new StorageAllocationDocument { StorageId = storageId, StorageType = typeStr, Quantity = quantity };
        var pushUpdate = Builders<BulkPieceDocument>.Update
            .Push(x => x.StorageAllocations, newAlloc)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, pushUpdate, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece?> RemoveStorageAsync(Guid id, Guid storageId)
    {
        var update = Builders<BulkPieceDocument>.Update
            .PullFilter(x => x.StorageAllocations, a => a.StorageId == storageId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<BulkPieceDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece?> ClearStorageAsync(Guid id)
    {
        var update = Builders<BulkPieceDocument>.Update
            .Set(x => x.StorageAllocations, new List<StorageAllocationDocument>())
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<BulkPieceDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    private static BulkPiece ToModel(BulkPieceDocument doc) => new()
    {
        Id = doc.Id,
        LegoId = doc.LegoId,
        LegoColor = doc.LegoColor,
        Description = doc.Description,
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

    private static BulkPieceDocument ToDocument(BulkPiece model) => new()
    {
        Id = model.Id,
        LegoId = model.LegoId,
        LegoColor = model.LegoColor,
        Description = model.Description,
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
