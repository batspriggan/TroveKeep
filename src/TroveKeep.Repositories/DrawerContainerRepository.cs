using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class DrawerContainerRepository : IDrawerContainerRepository
{
    private readonly IMongoCollection<DrawerContainerDocument> _containers;
    private readonly IMongoCollection<DrawerDocument> _drawers;
    private readonly IMongoCollection<BulkPieceDocument> _bulkPieces;

    public DrawerContainerRepository(IMongoDatabase database)
    {
        _containers = database.GetCollection<DrawerContainerDocument>("drawercontainers");
        _drawers = database.GetCollection<DrawerDocument>("drawers");
        _bulkPieces = database.GetCollection<BulkPieceDocument>("bulkpieces");
    }

    public async Task<IEnumerable<DrawerContainer>> GetAllAsync()
    {
        var containerDocs = await _containers.Find(_ => true).ToListAsync();
        if (containerDocs.Count == 0) return [];

        var containerIds = containerDocs.Select(c => c.Id).ToList();
        var drawerDocs = await _drawers.Find(x => containerIds.Contains(x.DrawerContainerId)).ToListAsync();
        var drawersByContainer = drawerDocs.GroupBy(d => d.DrawerContainerId).ToDictionary(g => g.Key, g => g.ToList());

        var allDrawerIds = drawerDocs.Select(d => d.Id).ToList();
        var pieceCountByDrawer = await CountPiecesPerDrawerAsync(allDrawerIds);

        return containerDocs.Select(c => ToModel(c, drawersByContainer.GetValueOrDefault(c.Id) ?? [], pieceCountByDrawer));
    }

    public async Task<DrawerContainer?> GetByIdAsync(Guid id)
    {
        var doc = await _containers.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (doc is null) return null;

        var drawerDocs = await _drawers.Find(x => x.DrawerContainerId == id).ToListAsync();
        var pieceCountByDrawer = await CountPiecesPerDrawerAsync(drawerDocs.Select(d => d.Id).ToList());
        return ToModel(doc, drawerDocs, pieceCountByDrawer);
    }

    public async Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    public async Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer)
    {
        var doc = ToDocument(drawerContainer);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _containers.InsertOneAsync(doc);
        return ToModel(doc, [], []);
    }

    public async Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer)
    {
        var existing = await _containers.Find(x => x.Id == drawerContainer.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(drawerContainer);
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        await _containers.ReplaceOneAsync(x => x.Id == drawerContainer.Id, doc);

        var drawerDocs = await _drawers.Find(x => x.DrawerContainerId == drawerContainer.Id).ToListAsync();
        var pieceCountByDrawer = await CountPiecesPerDrawerAsync(drawerDocs.Select(d => d.Id).ToList());
        return ToModel(doc, drawerDocs, pieceCountByDrawer);
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
        return docs.Select(d => ToModel(d, [], []));
    }

    private async Task<Dictionary<Guid, int>> CountPiecesPerDrawerAsync(List<Guid> drawerIds)
    {
        if (drawerIds.Count == 0) return [];
        var filter = Builders<BulkPieceDocument>.Filter.ElemMatch(
            x => x.StorageAllocations,
            a => a.StorageType == "Drawer" && drawerIds.Contains(a.StorageId));
        var pieces = await _bulkPieces.Find(filter).ToListAsync();
        var counts = new Dictionary<Guid, int>();
        foreach (var piece in pieces)
            foreach (var alloc in piece.StorageAllocations)
                if (alloc.StorageType == "Drawer" && drawerIds.Contains(alloc.StorageId))
                    counts[alloc.StorageId] = counts.GetValueOrDefault(alloc.StorageId) + 1;
        return counts;
    }

    private static DrawerContainer ToModel(DrawerContainerDocument doc, List<DrawerDocument> drawerDocs, Dictionary<Guid, int> pieceCountByDrawer) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Drawers = drawerDocs.Select(d => new Drawer
        {
            Id = d.Id,
            Position = d.Position,
            Label = d.Label,
            DrawerContainerId = d.DrawerContainerId,
            BulkPieces = Enumerable.Range(0, pieceCountByDrawer.GetValueOrDefault(d.Id)).Select(_ => new BulkPiece()).ToList(),
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(d.UpdatedAt, DateTimeKind.Utc)),
        }).ToList(),
    };

    private static DrawerContainerDocument ToDocument(DrawerContainer model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
