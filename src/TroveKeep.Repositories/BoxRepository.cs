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

        var setFilter = Builders<LegoSetDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            Builders<StorageAllocationDocument>.Filter.And(
                Builders<StorageAllocationDocument>.Filter.In(a => a.StorageId, boxIds),
                Builders<StorageAllocationDocument>.Filter.Eq(a => a.StorageType, "Box")));
        var setDocs = await _legoSets.Find(setFilter).ToListAsync();

        var pieceFilter = Builders<BulkPieceDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            Builders<StorageAllocationDocument>.Filter.And(
                Builders<StorageAllocationDocument>.Filter.In(a => a.StorageId, boxIds),
                Builders<StorageAllocationDocument>.Filter.Eq(a => a.StorageType, "Box")));
        var pieceDocs = await _bulkPieces.Find(pieceFilter).ToListAsync();

        // A set/piece can appear in multiple boxes; build per-box lists
        var setsByBox = new Dictionary<Guid, List<LegoSetDocument>>();
        foreach (var setDoc in setDocs)
        {
            foreach (var alloc in setDoc.StorageAllocations.Where(a => a.StorageType == "Box" && boxIds.Contains(a.StorageId)))
            {
                if (!setsByBox.TryGetValue(alloc.StorageId, out var list))
                    setsByBox[alloc.StorageId] = list = [];
                list.Add(setDoc);
            }
        }

        var piecesByBox = new Dictionary<Guid, List<BulkPieceDocument>>();
        foreach (var pieceDoc in pieceDocs)
        {
            foreach (var alloc in pieceDoc.StorageAllocations.Where(a => a.StorageType == "Box" && boxIds.Contains(a.StorageId)))
            {
                if (!piecesByBox.TryGetValue(alloc.StorageId, out var list))
                    piecesByBox[alloc.StorageId] = list = [];
                list.Add(pieceDoc);
            }
        }

        return boxDocs.Select(b => ToModel(b,
            setsByBox.GetValueOrDefault(b.Id) ?? [],
            piecesByBox.GetValueOrDefault(b.Id) ?? []));
    }

    public async Task<Box?> GetByIdAsync(Guid id)
    {
        var doc = await _boxes.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (doc is null) return null;

        var setFilter = Builders<LegoSetDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            a => a.StorageId == id && a.StorageType == "Box");
        var sets = await _legoSets.Find(setFilter).ToListAsync();

        var pieceFilter = Builders<BulkPieceDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            a => a.StorageId == id && a.StorageType == "Box");
        var pieces = await _bulkPieces.Find(pieceFilter).ToListAsync();

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

        var setFilter = Builders<LegoSetDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            a => a.StorageId == box.Id && a.StorageType == "Box");
        var sets = await _legoSets.Find(setFilter).ToListAsync();

        var pieceFilter = Builders<BulkPieceDocument>.Filter.ElemMatch(x => x.StorageAllocations,
            a => a.StorageId == box.Id && a.StorageType == "Box");
        var pieces = await _bulkPieces.Find(pieceFilter).ToListAsync();

        return ToModel(doc, sets, pieces);
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
        return docs.Select(d => ToModel(d, [], []));
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
            StorageAllocations = s.StorageAllocations.Select(a => new StorageAllocation
            {
                StorageId = a.StorageId,
                Type = Enum.Parse<StorageType>(a.StorageType),
                Quantity = a.Quantity,
            }).ToList(),
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
            StorageAllocations = p.StorageAllocations.Select(a => new StorageAllocation
            {
                StorageId = a.StorageId,
                Type = Enum.Parse<StorageType>(a.StorageType),
                Quantity = a.Quantity,
            }).ToList(),
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
