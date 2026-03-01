using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class PartCategoryRepository : IPartCategoryRepository
{
    private readonly IMongoCollection<PartCategoryDocument> _collection;
    private readonly IMongoCollection<ArchiveMetaDocument> _meta;

    public PartCategoryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PartCategoryDocument>("rebrickable_part_categories");
        _meta = database.GetCollection<ArchiveMetaDocument>("archive_meta");
    }

    public async Task<IEnumerable<RebrickablePartCategory>> GetAllAsync()
    {
        var docs = await _collection.Find(_ => true).SortBy(x => x.Id).ToListAsync();
        return docs.Select(d => new RebrickablePartCategory { Id = d.Id, Name = d.Name });
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<RebrickablePartCategory> categories)
    {
        var docs = categories.Select(c => new PartCategoryDocument { Id = c.Id, Name = c.Name }).ToList();
        await _collection.DeleteManyAsync(_ => true);
        if (docs.Count > 0)
            await _collection.InsertManyAsync(docs);

        var now = DateTime.UtcNow;
        await _meta.ReplaceOneAsync(
            x => x.Key == "part_categories",
            new ArchiveMetaDocument { Key = "part_categories", LastImportedAt = now },
            new ReplaceOptions { IsUpsert = true });

        return docs.Count;
    }

    public async Task<DateTimeOffset?> GetLastImportedAtAsync()
    {
        var doc = await _meta.Find(x => x.Key == "part_categories").FirstOrDefaultAsync();
        if (doc is null) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(doc.LastImportedAt, DateTimeKind.Utc));
    }
}
