using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class DrawerRepository : IDrawerRepository
{
    private readonly IMongoCollection<DrawerContainerDocument> _containers;
    private readonly ICollectionScope _scope;

    public DrawerRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _containers = database.GetCollection<DrawerContainerDocument>("drawercontainers");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<Drawer?> GetByPositionAsync(Guid containerId, int position)
    {
        // Drawers are embedded in their container, so isolation is the container's collection.
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(x => x.Id, containerId));
        var container = await _containers.Find(filter).FirstOrDefaultAsync();
        if (container is null) return null;
        var sub = container.Drawers.FirstOrDefault(d => d.Position == position);
        return sub is null ? null : ToModel(sub, containerId);
    }

    public async Task<Drawer?> GetByPositionWithContentsAsync(Guid containerId, int position)
    {
        return await GetByPositionAsync(containerId, position);
    }

    public async Task<Drawer> CreateAsync(Drawer drawer)
    {
        var now = DateTime.UtcNow;
        var sub = new DrawerDocument
        {
            Position = drawer.Position,
            Label = drawer.Label,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var push = Builders<DrawerContainerDocument>.Update.Push(x => x.Drawers, sub);
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(c => c.Id, drawer.DrawerContainerId));
        await _containers.UpdateOneAsync(filter, push);
        return ToModel(sub, drawer.DrawerContainerId);
    }

    public async Task<Drawer?> UpdateAsync(Drawer drawer)
    {
        var arrayFilter = new BsonDocumentArrayFilterDefinition<BsonDocument>(
            new BsonDocument("elem.Position", drawer.Position));
        var update = Builders<DrawerContainerDocument>.Update
            .Set("Drawers.$[elem].Label", drawer.Label)
            .Set("Drawers.$[elem].UpdatedAt", DateTime.UtcNow);
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(c => c.Id, drawer.DrawerContainerId));
        var result = await _containers.UpdateOneAsync(
            filter, update,
            new UpdateOptions { ArrayFilters = [arrayFilter] });
        if (result.MatchedCount == 0) return null;
        return drawer;
    }

    public async Task<bool> DeleteAsync(Guid containerId, int position)
    {
        var pull = Builders<DrawerContainerDocument>.Update.PullFilter(
            x => x.Drawers, Builders<DrawerDocument>.Filter.Eq(d => d.Position, position));
        var filter = _scope.ScopedTo<DrawerContainerDocument>(d => d.CollectionId,
            Builders<DrawerContainerDocument>.Filter.Eq(c => c.Id, containerId));
        var result = await _containers.UpdateOneAsync(filter, pull);
        return result.ModifiedCount > 0;
    }

    private static Drawer ToModel(DrawerDocument doc, Guid containerId) => new()
    {
        Position = doc.Position,
        Label = doc.Label,
        DrawerContainerId = containerId,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };
}
