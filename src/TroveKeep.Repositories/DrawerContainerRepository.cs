using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class DrawerContainerRepository : IDrawerContainerRepository
{
    private readonly IMongoCollection<DrawerContainerDocument> _containers;

    public DrawerContainerRepository(IMongoDatabase database)
    {
        _containers = database.GetCollection<DrawerContainerDocument>("drawercontainers");
    }

    public async Task<IEnumerable<DrawerContainer>> GetAllAsync()
    {
        var docs = await _containers.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<DrawerContainer?> GetByIdAsync(Guid id)
    {
        var doc = await _containers.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer)
    {
        var now = DateTime.UtcNow;
        var doc = new DrawerContainerDocument
        {
            Id = Guid.NewGuid(),
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
        };
        await _containers.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer)
    {
        var existing = await _containers.Find(x => x.Id == drawerContainer.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = new DrawerContainerDocument
        {
            Id = drawerContainer.Id,
            Name = drawerContainer.Name,
            Description = drawerContainer.Description,
            Drawers = existing.Drawers,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
        };
        await _containers.ReplaceOneAsync(x => x.Id == drawerContainer.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _containers.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<IEnumerable<DrawerContainer>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var docs = await _containers.Find(x => idList.Contains(x.Id)).ToListAsync();
        return docs.Select(ToModel);
    }

    private static DrawerContainer ToModel(DrawerContainerDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
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
