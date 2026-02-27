using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class ColorRepository : IColorRepository
{
    private readonly IMongoCollection<ColorDocument> _collection;
    private readonly IMongoCollection<ArchiveMetaDocument> _meta;

    public ColorRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ColorDocument>("rebrickable_colors");
        _meta = database.GetCollection<ArchiveMetaDocument>("archive_meta");
    }

    public async Task<IEnumerable<RebrickableColor>> GetAllAsync()
    {
        var docs = await _collection.Find(_ => true).SortBy(x => x.Id).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<RebrickableColor> colors)
    {
        var docs = colors.Select(ToDocument).ToList();
        await _collection.DeleteManyAsync(_ => true);
        if (docs.Count > 0)
            await _collection.InsertManyAsync(docs);

        var now = DateTime.UtcNow;
        await _meta.ReplaceOneAsync(
            x => x.Key == "colors",
            new ArchiveMetaDocument { Key = "colors", LastImportedAt = now },
            new ReplaceOptions { IsUpsert = true });

        return docs.Count;
    }

    public async Task<DateTimeOffset?> GetLastImportedAtAsync()
    {
        var doc = await _meta.Find(x => x.Key == "colors").FirstOrDefaultAsync();
        if (doc is null) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(doc.LastImportedAt, DateTimeKind.Utc));
    }

    private static RebrickableColor ToModel(ColorDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Rgb = doc.Rgb,
        IsTrans = doc.IsTrans,
        StartYear = doc.StartYear,
        EndYear = doc.EndYear,
    };

    private static ColorDocument ToDocument(RebrickableColor model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Rgb = model.Rgb,
        IsTrans = model.IsTrans,
        StartYear = model.StartYear,
        EndYear = model.EndYear,
    };
}
