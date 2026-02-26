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

    public async Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId)
    {
        var update = Builders<LegoSetDocument>.Update
            .Set(x => x.BoxId, boxId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<LegoSetDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    public async Task<LegoSet?> RemoveStorageAsync(Guid id)
    {
        var update = Builders<LegoSetDocument>.Update
            .Unset(x => x.BoxId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var options = new FindOneAndUpdateOptions<LegoSetDocument> { ReturnDocument = ReturnDocument.After };
        var doc = await _collection.FindOneAndUpdateAsync(x => x.Id == id, update, options);
        return doc is null ? null : ToModel(doc);
    }

    private static LegoSet ToModel(LegoSetDocument doc) => new()
    {
        Id = doc.Id,
        SetNumber = doc.SetNumber,
        Description = doc.Description,
        PhotoUrl = doc.PhotoUrl,
        BoxId = doc.BoxId,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static LegoSetDocument ToDocument(LegoSet model) => new()
    {
        Id = model.Id,
        SetNumber = model.SetNumber,
        Description = model.Description,
        PhotoUrl = model.PhotoUrl,
        BoxId = model.BoxId,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
