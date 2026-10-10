using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly IMongoCollection<CollectionDocument> _collections;

    public CollectionRepository(IMongoDatabase database)
    {
        _collections = database.GetCollection<CollectionDocument>("collections");
    }

    public async Task<IEnumerable<Collection>> GetAllAsync()
    {
        var docs = await _collections.Find(_ => true).SortBy(x => x.Name).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Collection?> GetByIdAsync(Guid id)
    {
        var doc = await _collections.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Collection> CreateAsync(Collection collection)
    {
        var doc = ToDocument(collection);
        doc.Id = collection.Id == Guid.Empty ? Guid.NewGuid() : collection.Id;
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _collections.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Collection?> UpdateAsync(Collection collection)
    {
        var existing = await _collections.Find(x => x.Id == collection.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(collection);
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;

        await _collections.ReplaceOneAsync(x => x.Id == collection.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _collections.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _collections.Find(x => x.Id == id).AnyAsync();

    private static Collection ToModel(CollectionDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static CollectionDocument ToDocument(Collection model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
