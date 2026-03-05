using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BoxRepository : IBoxRepository
{
    private readonly IMongoCollection<BoxDocument> _boxes;

    public BoxRepository(IMongoDatabase database)
    {
        _boxes = database.GetCollection<BoxDocument>("boxes");
    }

    public async Task<IEnumerable<Box>> GetAllAsync()
    {
        var docs = await _boxes.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Box?> GetByIdAsync(Guid id)
    {
        var doc = await _boxes.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Box?> GetByIdWithContentsAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<Box> CreateAsync(Box box)
    {
        var doc = ToDocument(box);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _boxes.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Box?> UpdateAsync(Box box)
    {
        var existing = await _boxes.Find(x => x.Id == box.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(box);
        doc.CreatedAt = existing.CreatedAt;
        doc.ImageCached = existing.ImageCached;
        doc.UpdatedAt = DateTime.UtcNow;
        await _boxes.ReplaceOneAsync(x => x.Id == box.Id, doc);
        return ToModel(doc);
    }

    public async Task UpdateImageCachedAsync(Guid id, bool cached)
    {
        var update = Builders<BoxDocument>.Update.Set(x => x.ImageCached, cached);
        await _boxes.UpdateOneAsync(x => x.Id == id, update);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _boxes.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<IEnumerable<Box>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var docs = await _boxes.Find(x => idList.Contains(x.Id)).ToListAsync();
        return docs.Select(ToModel);
    }

    private static Box ToModel(BoxDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static BoxDocument ToDocument(Box model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        ImageCached = model.ImageCached,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
