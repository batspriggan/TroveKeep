using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
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

    public async Task<IEnumerable<LegoSet>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var docs = await _collection.Find(x => idList.Contains(x.Id)).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<LegoSet> CreateAsync(LegoSet legoSet)
    {
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
        var existing = await _collection.Find(x => x.Id == legoSet.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(legoSet);
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
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task UpdateImageCachedAsync(Guid id)
    {
        var update = Builders<LegoSetDocument>.Update.Set(x => x.ImageCached, true);
        await _collection.UpdateOneAsync(x => x.Id == id, update);
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
