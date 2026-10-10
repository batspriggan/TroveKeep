using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BoxRepository : IBoxRepository
{
    private readonly IMongoCollection<BoxDocument> _boxes;
    private readonly ICollectionScope _scope;

    public BoxRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _boxes = database.GetCollection<BoxDocument>("boxes");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<Box>> GetAllAsync()
    {
        var docs = await _boxes
            .Find(_scope.ScopedTo<BoxDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Box?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedToAccessible<BoxDocument>(d => d.CollectionId,
            Builders<BoxDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _boxes.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Box?> GetByIdWithContentsAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<Box> CreateAsync(Box box)
    {
        box.CollectionId = _scope.ResolveCreateCollectionId(box.CollectionId);
        var doc = ToDocument(box);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _boxes.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Box?> UpdateAsync(Box box)
    {
        var scopeFilter = _scope.ScopedTo<BoxDocument>(d => d.CollectionId,
            Builders<BoxDocument>.Filter.Eq(x => x.Id, box.Id));
        var existing = await _boxes.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(box);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
        doc.CreatedAt = existing.CreatedAt;
        doc.ImageCached = existing.ImageCached;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _boxes.ReplaceOneAsync(
            x => x.Id == box.Id && x.Version == box.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Box {box.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task UpdateImageCachedAsync(Guid id, bool cached)
    {
        var filter = _scope.ScopedTo<BoxDocument>(d => d.CollectionId,
            Builders<BoxDocument>.Filter.Eq(x => x.Id, id));
        var update = Builders<BoxDocument>.Update.Set(x => x.ImageCached, cached);
        await _boxes.UpdateOneAsync(filter, update);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filter = _scope.ScopedTo<BoxDocument>(d => d.CollectionId,
            Builders<BoxDocument>.Filter.Eq(x => x.Id, id));
        var result = await _boxes.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<IEnumerable<Box>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var filter = _scope.ScopedTo<BoxDocument>(d => d.CollectionId,
            Builders<BoxDocument>.Filter.In(x => x.Id, idList));
        var docs = await _boxes.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    private static Box ToModel(BoxDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        Name = doc.Name,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static BoxDocument ToDocument(Box model) => new()
    {
        Id = model.Id,
        CollectionId = model.CollectionId,
        Name = model.Name,
        ImageCached = model.ImageCached,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
