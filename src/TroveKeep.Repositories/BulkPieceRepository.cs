using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
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

    public async Task<IEnumerable<BulkPiece>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var docs = await _collection.Find(x => idList.Contains(x.Id)).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<BulkPiece> CreateAsync(BulkPiece bulkPiece)
    {
        var doc = ToDocument(bulkPiece);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _collection.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece)
    {
        var existing = await _collection.Find(x => x.Id == bulkPiece.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(bulkPiece);
        doc.ImageCached = existing.ImageCached;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _collection.ReplaceOneAsync(
            x => x.Id == bulkPiece.Id && x.Version == bulkPiece.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Piece {bulkPiece.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<(IEnumerable<BulkPiece> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var filter = string.IsNullOrWhiteSpace(query)
            ? Builders<BulkPieceDocument>.Filter.Empty
            : Builders<BulkPieceDocument>.Filter.Or(
                Builders<BulkPieceDocument>.Filter.Regex(d => d.LegoId, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")),
                Builders<BulkPieceDocument>.Filter.Regex(d => d.Description, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")));

        var total = await _collection.CountDocumentsAsync(filter);
        var docs = await _collection.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
        return (docs.Select(ToModel), total);
    }

    public async Task<IEnumerable<BulkPiece>> SearchAsync(string query)
    {
        var regex = new BsonRegularExpression(query, "i");
        var filter = Builders<BulkPieceDocument>.Filter.Or(
            Builders<BulkPieceDocument>.Filter.Regex(d => d.LegoId, regex),
            Builders<BulkPieceDocument>.Filter.Regex(d => d.Description, regex));
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task UpdateImageCachedAsync(Guid id)
    {
        var filter = Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, id);
        var update = Builders<BulkPieceDocument>.Update.Set(x => x.ImageCached, true);
        await _collection.UpdateOneAsync(filter, update);
    }

    private static BulkPiece ToModel(BulkPieceDocument doc) => new()
    {
        Id = doc.Id,
        LegoId = doc.LegoId,
        LegoColorId = doc.LegoColorId,
        Description = doc.Description,
        Quantity = doc.Quantity,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        ImageCached = doc.ImageCached,
        Version = doc.Version,
    };

    private static BulkPieceDocument ToDocument(BulkPiece model) => new()
    {
        Id = model.Id,
        LegoId = model.LegoId,
        LegoColorId = model.LegoColorId,
        Description = model.Description,
        Quantity = model.Quantity,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        ImageCached = model.ImageCached,
        Version = model.Version,
    };
}
