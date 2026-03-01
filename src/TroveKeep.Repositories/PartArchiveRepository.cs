using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class PartArchiveRepository : IPartArchiveRepository
{
    private readonly IMongoCollection<PartArchiveDocument> _collection;
    private readonly IMongoCollection<ArchiveMetaDocument> _meta;

    public PartArchiveRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PartArchiveDocument>("rebrickable_parts");
        _meta = database.GetCollection<ArchiveMetaDocument>("archive_meta");
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<RebrickablePart> parts)
    {
        var docs = parts.Select(ToDocument).ToList();
        await _collection.DeleteManyAsync(_ => true);
        if (docs.Count > 0)
            await _collection.InsertManyAsync(docs);

        var now = DateTime.UtcNow;
        await _meta.ReplaceOneAsync(
            x => x.Key == "parts",
            new ArchiveMetaDocument { Key = "parts", LastImportedAt = now },
            new ReplaceOptions { IsUpsert = true });

        return docs.Count;
    }

    public async Task<DateTimeOffset?> GetLastImportedAtAsync()
    {
        var doc = await _meta.Find(x => x.Key == "parts").FirstOrDefaultAsync();
        if (doc is null) return null;
        return new DateTimeOffset(DateTime.SpecifyKind(doc.LastImportedAt, DateTimeKind.Utc));
    }

    public async Task<int> GetCountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }

    public async Task<IEnumerable<RebrickablePart>> SearchAsync(string query, int limit)
    {
        var escaped = Regex.Escape(query);
        var regex = new BsonRegularExpression(escaped, "i");
        var filter = Builders<PartArchiveDocument>.Filter.Or(
            Builders<PartArchiveDocument>.Filter.Regex(x => x.PartNum, regex),
            Builders<PartArchiveDocument>.Filter.Regex(x => x.Name, regex));
        var docs = await _collection.Find(filter).Limit(limit).ToListAsync();
        return docs.Select(ToModel);
    }

    private static RebrickablePart ToModel(PartArchiveDocument doc) => new(
        PartNum : doc.PartNum,
        Name : doc.Name,
        PartCategoryId : doc.PartCategoryId
    );

    private static PartArchiveDocument ToDocument(RebrickablePart model) => new()
    {
        PartNum = model.PartNum,
        Name = model.Name,
        PartCategoryId = model.PartCategoryId
    };
}
