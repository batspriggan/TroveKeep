using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class SetArchiveRepository : ISetArchiveRepository
{
    private readonly IMongoCollection<SetArchiveDocument> _collection;
    private readonly IMongoCollection<ArchiveMetaDocument> _meta;

    public SetArchiveRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SetArchiveDocument>("rebrickable_sets");
        _meta = database.GetCollection<ArchiveMetaDocument>("archive_meta");
    }

    public async Task<IEnumerable<RebrickableSet>> GetAllAsync()
    {
        var docs = await _collection.Find(_ => true).SortBy(x => x.SetNum).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<RebrickableSet> sets)
    {
        var docs = sets.Select(ToDocument).ToList();
        await _collection.DeleteManyAsync(_ => true);
        if (docs.Count > 0)
            await _collection.InsertManyAsync(docs);

        var now = DateTime.UtcNow;
        await _meta.ReplaceOneAsync(
            x => x.Key == "sets",
            new ArchiveMetaDocument { Key = "sets", LastImportedAt = now },
            new ReplaceOptions { IsUpsert = true });

        return docs.Count;
    }

    public async Task<DateTimeOffset?> GetLastImportedAtAsync()
    {
        var doc = await _meta.Find(x => x.Key == "sets").FirstOrDefaultAsync();
        if (doc is null) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(doc.LastImportedAt, DateTimeKind.Utc));
    }

    private static RebrickableSet ToModel(SetArchiveDocument doc) => new()
    {
        SetNum = doc.SetNum,
        Name = doc.Name,
        Year = doc.Year,
        ThemeId = doc.ThemeId,
        NumParts = doc.NumParts,
        ImgUrl = doc.ImgUrl,
    };

    private static SetArchiveDocument ToDocument(RebrickableSet model) => new()
    {
        SetNum = model.SetNum,
        Name = model.Name,
        Year = model.Year,
        ThemeId = model.ThemeId,
        NumParts = model.NumParts,
        ImgUrl = model.ImgUrl,
    };
}
