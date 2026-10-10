using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class DrawerContainerRepository : IDrawerContainerRepository
{
    private readonly IMongoCollection<DrawerContainerDocument> _containers;
    private readonly ICollectionScope _scope;

    public DrawerContainerRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _containers = database.GetCollection<DrawerContainerDocument>("drawercontainers");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<DrawerContainer>> GetAllAsync()
    {
        var docs = await _containers
            .Find(_scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<DrawerContainer?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedToAccessible<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _containers.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer)
    {
        drawerContainer.CollectionId = _scope.ResolveCreateCollectionId(drawerContainer.CollectionId);
        var now = DateTime.UtcNow;
        var doc = new DrawerContainerDocument
        {
            Id = Guid.NewGuid(),
            CollectionId = drawerContainer.CollectionId,
            Name = drawerContainer.Name,
            Description = drawerContainer.Description,
            Drawers = drawerContainer.Drawers.Select(d => new DrawerDocument
            {
                Position = d.Position,
                Label = d.Label,
                CreatedAt = now,
                UpdatedAt = now,
            }).ToList(),
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
        await _containers.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer)
    {
        var scopeFilter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(x => x.Id, drawerContainer.Id));
        var existing = await _containers.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = new DrawerContainerDocument
        {
            Id = drawerContainer.Id,
            // A write never moves a row across the isolation boundary.
            CollectionId = existing.CollectionId,
            Name = drawerContainer.Name,
            Description = drawerContainer.Description,
            ImageCached = existing.ImageCached,
            Drawers = existing.Drawers,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Version = existing.Version + 1,
        };

        var result = await _containers.ReplaceOneAsync(
            x => x.Id == drawerContainer.Id && x.Version == drawerContainer.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Container {drawerContainer.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task UpdateImageCachedAsync(Guid id, bool cached)
    {
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(x => x.Id, id));
        var update = Builders<DrawerContainerDocument>.Update.Set(x => x.ImageCached, cached);
        await _containers.UpdateOneAsync(filter, update);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(x => x.Id, id));
        var result = await _containers.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<IEnumerable<DrawerContainer>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.In(x => x.Id, idList));
        var docs = await _containers.Find(filter).ToListAsync();
        return docs.Select(ToModel);
    }

    private static DrawerContainer ToModel(DrawerContainerDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        Name = doc.Name,
        Description = doc.Description,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
        Drawers = doc.Drawers.Select(d => new Drawer
        {
            Position = d.Position,
            Label = d.Label,
            DrawerContainerId = doc.Id,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(d.UpdatedAt, DateTimeKind.Utc)),
        }).ToList(),
    };
}
