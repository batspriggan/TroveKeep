using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class LegoSetRepository : ILegoSetRepository
{
    private readonly IMongoCollection<LegoSetDocument> _collection;
    private readonly ICollectionScope _scope;

    public LegoSetRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _collection = database.GetCollection<LegoSetDocument>("legosets");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<LegoSet>> GetAllAsync()
    {
        var docs = await _collection
            .Find(_scope.ScopedTo<LegoSetDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<LegoSet?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedToAccessible<LegoSetDocument>(d => d.CollectionId,
            Builders<LegoSetDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<IEnumerable<LegoSet>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var filter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId,
            Builders<LegoSetDocument>.Filter.In(x => x.Id, idList));
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<LegoSet> CreateAsync(LegoSet legoSet)
    {
        legoSet.CollectionId = _scope.ResolveCreateCollectionId(legoSet.CollectionId);
        var doc = ToDocument(legoSet);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _collection.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<LegoSet?> UpdateAsync(LegoSet legoSet)
    {
        var scopeFilter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId,
            Builders<LegoSetDocument>.Filter.Eq(x => x.Id, legoSet.Id));
        var existing = await _collection.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(legoSet);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
        doc.ImageCached = existing.ImageCached;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _collection.ReplaceOneAsync(
            x => x.Id == legoSet.Id && x.Version == legoSet.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Set {legoSet.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId,
            Builders<LegoSetDocument>.Filter.Eq(x => x.Id, id));
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task UpdateImageCachedAsync(Guid id)
    {
        var filter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId,
            Builders<LegoSetDocument>.Filter.Eq(x => x.Id, id));
        var update = Builders<LegoSetDocument>.Update.Set(x => x.ImageCached, true);
        await _collection.UpdateOneAsync(filter, update);
    }

    public async Task<(IEnumerable<LegoSet> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var filter = string.IsNullOrWhiteSpace(query)
            ? Builders<LegoSetDocument>.Filter.Empty
            : Builders<LegoSetDocument>.Filter.Or(
                Builders<LegoSetDocument>.Filter.Regex(d => d.SetNumber, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")),
                Builders<LegoSetDocument>.Filter.Regex(d => d.Description, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query), "i")));
        filter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId, filter);

        var total = await _collection.CountDocumentsAsync(filter);
        var docs = await _collection.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
        return (docs.Select(ToModel), total);
    }

    public async Task<IEnumerable<LegoSet>> SearchAsync(string query)
    {
        var regex = new BsonRegularExpression(query, "i");
        var filter = Builders<LegoSetDocument>.Filter.Or(
            Builders<LegoSetDocument>.Filter.Regex(d => d.SetNumber, regex),
            Builders<LegoSetDocument>.Filter.Regex(d => d.Description, regex));
        filter = _scope.ScopedTo<LegoSetDocument>(d => d.CollectionId, filter);
        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    private static LegoSet ToModel(LegoSetDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        SetNumber = doc.SetNumber,
        Description = doc.Description,
        Quantity = doc.Quantity,
        IsMoc = doc.IsMoc,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static LegoSetDocument ToDocument(LegoSet model) => new()
    {
        Id = model.Id,
        CollectionId = model.CollectionId,
        SetNumber = model.SetNumber,
        Description = model.Description,
        Quantity = model.Quantity,
        IsMoc = model.IsMoc,
        ImageCached = model.ImageCached,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
