using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BulkPieceRepository : IBulkPieceRepository
{
    private readonly IMongoCollection<BulkPieceDocument> _collection;
    private readonly ICollectionScope _scope;

    public BulkPieceRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _collection = database.GetCollection<BulkPieceDocument>("bulkpieces");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<BulkPiece>> GetAllAsync()
    {
        var docs = await _collection
            .Find(_scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<BulkPiece?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedToAccessible<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<BulkPiece?> GetByBusinessKeyAsync(string legoId, int legoColorId)
    {
        var filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.Where(x => x.LegoId == legoId && x.LegoColorId == legoColorId));
        var doc = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<IEnumerable<BulkPiece>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.In(x => x.Id, idList));
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<BulkPiece> CreateAsync(BulkPiece bulkPiece)
    {
        bulkPiece.CollectionId = _scope.ResolveCreateCollectionId(bulkPiece.CollectionId);
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
        var scopeFilter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, bulkPiece.Id));
        var existing = await _collection.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(bulkPiece);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
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
        var filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, id));
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<(IEnumerable<BulkPiece> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var filter = string.IsNullOrWhiteSpace(query)
            ? Builders<BulkPieceDocument>.Filter.Empty
            : Builders<BulkPieceDocument>.Filter.Or(
                Builders<BulkPieceDocument>.Filter.Regex(d => d.LegoId, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")),
                Builders<BulkPieceDocument>.Filter.Regex(d => d.Description, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")));
        filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId, filter);

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
        filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId, filter);
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task UpdateImageCachedAsync(Guid id)
    {
        var filter = _scope.ScopedTo<BulkPieceDocument>(d => d.CollectionId,
            Builders<BulkPieceDocument>.Filter.Eq(x => x.Id, id));
        var update = Builders<BulkPieceDocument>.Update.Set(x => x.ImageCached, true);
        await _collection.UpdateOneAsync(filter, update);
    }

    private static BulkPiece ToModel(BulkPieceDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
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
        CollectionId = model.CollectionId,
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
