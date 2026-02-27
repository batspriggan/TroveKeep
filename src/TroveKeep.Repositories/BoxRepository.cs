using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BoxRepository : IBoxRepository
{
    private readonly IMongoCollection<BoxDocument> _boxes;
    private readonly IMongoCollection<LegoSetDocument> _legoSets;
    private readonly IMongoCollection<BulkPieceDocument> _bulkPieces;

    public BoxRepository(IMongoDatabase database)
    {
        _boxes = database.GetCollection<BoxDocument>("boxes");
        _legoSets = database.GetCollection<LegoSetDocument>("legosets");
        _bulkPieces = database.GetCollection<BulkPieceDocument>("bulkpieces");
    }

    public async Task<IEnumerable<Box>> GetAllAsync()
    {
        var boxDocs = await _boxes.Find(_ => true).ToListAsync();
        if (boxDocs.Count == 0) return [];

        var boxIds = boxDocs.Select(b => b.Id).ToList();

        var setDocs = await _legoSets.Find(x => x.BoxId.HasValue && boxIds.Contains(x.BoxId.Value)).ToListAsync();
        var pieceDocs = await _bulkPieces.Find(x => x.BoxId.HasValue && boxIds.Contains(x.BoxId.Value)).ToListAsync();

        var setsByBox = setDocs.GroupBy(s => s.BoxId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var piecesByBox = pieceDocs.GroupBy(p => p.BoxId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        return boxDocs.Select(b => ToModel(b,
            setsByBox.GetValueOrDefault(b.Id) ?? [],
            piecesByBox.GetValueOrDefault(b.Id) ?? []));
    }

    public async Task<Box?> GetByIdAsync(Guid id)
    {
        var doc = await _boxes.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (doc is null) return null;

        var sets = await _legoSets.Find(x => x.BoxId == id).ToListAsync();
        var pieces = await _bulkPieces.Find(x => x.BoxId == id).ToListAsync();
        return ToModel(doc, sets, pieces);
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
        return ToModel(doc, [], []);
    }

    public async Task<Box?> UpdateAsync(Box box)
    {
        var existing = await _boxes.Find(x => x.Id == box.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(box);
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        await _boxes.ReplaceOneAsync(x => x.Id == box.Id, doc);

        var sets = await _legoSets.Find(x => x.BoxId == box.Id).ToListAsync();
        var pieces = await _bulkPieces.Find(x => x.BoxId == box.Id).ToListAsync();
        return ToModel(doc, sets, pieces);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _boxes.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    private static Box ToModel(BoxDocument doc, List<LegoSetDocument> sets, List<BulkPieceDocument> pieces) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        PhotoUrl = doc.PhotoUrl,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Sets = sets.Select(s => new LegoSet
        {
            Id = s.Id,
            SetNumber = s.SetNumber,
            Description = s.Description,
            PhotoUrl = s.PhotoUrl,
            Quantity = s.Quantity,
            BoxId = s.BoxId,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(s.UpdatedAt, DateTimeKind.Utc)),
        }).ToList(),
        BulkPieces = pieces.Select(p => new BulkPiece
        {
            Id = p.Id,
            LegoId = p.LegoId,
            LegoColor = p.LegoColor,
            Description = p.Description,
            Quantity = p.Quantity,
            BoxId = p.BoxId,
            DrawerId = p.DrawerId,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(p.UpdatedAt, DateTimeKind.Utc)),
        }).ToList(),
    };

    private static BoxDocument ToDocument(Box model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        PhotoUrl = model.PhotoUrl,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
