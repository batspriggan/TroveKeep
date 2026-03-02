using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class DrawerRepository : IDrawerRepository
{
    private readonly IMongoCollection<DrawerDocument> _drawers;

    public DrawerRepository(IMongoDatabase database)
    {
        _drawers = database.GetCollection<DrawerDocument>("drawers");
    }

    public async Task<Drawer?> GetByIdAsync(Guid id)
    {
        var doc = await _drawers.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Drawer?> GetByIdWithContentsAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<Drawer> CreateAsync(Drawer drawer)
    {
        var doc = ToDocument(drawer);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _drawers.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Drawer?> UpdateAsync(Drawer drawer)
    {
        var existing = await _drawers.Find(x => x.Id == drawer.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(drawer);
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        await _drawers.ReplaceOneAsync(x => x.Id == drawer.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _drawers.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<IEnumerable<Drawer>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];
        var docs = await _drawers.Find(x => idList.Contains(x.Id)).ToListAsync();
        return docs.Select(ToModel);
    }

    private static Drawer ToModel(DrawerDocument doc) => new()
    {
        Id = doc.Id,
        Position = doc.Position,
        Label = doc.Label,
        DrawerContainerId = doc.DrawerContainerId,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static DrawerDocument ToDocument(Drawer model) => new()
    {
        Id = model.Id,
        Position = model.Position,
        Label = model.Label,
        DrawerContainerId = model.DrawerContainerId,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
