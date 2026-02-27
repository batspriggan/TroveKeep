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

    public async Task<BulkPiece?> AssignToBoxAsync(Guid id, Guid boxId)
    {
        var update = Builders<BulkPieceDocument>.Update
            .Set(x => x.BoxId, boxId)
            .Unset(x => x.DrawerId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<BulkPieceDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece?> AssignToDrawerAsync(Guid id, Guid drawerId)
    {
        var update = Builders<BulkPieceDocument>.Update
            .Set(x => x.DrawerId, drawerId)
            .Unset(x => x.BoxId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<BulkPieceDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece?> RemoveStorageAsync(Guid id)
    {
        var update = Builders<BulkPieceDocument>.Update
            .Unset(x => x.BoxId)
            .Unset(x => x.DrawerId)
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
        BoxId = doc.BoxId,
        DrawerId = doc.DrawerId,
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
        BoxId = model.BoxId,
        DrawerId = model.DrawerId,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
