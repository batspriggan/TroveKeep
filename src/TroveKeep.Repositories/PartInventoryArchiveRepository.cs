using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class PartInventoryArchiveRepository : IPartInventoryArchiveRepository
{
    private readonly IMongoCollection<PartInventoryArchiveDocument> _collection;
    private readonly IMongoCollection<ArchiveMetaDocument> _meta;

    public PartInventoryArchiveRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PartInventoryArchiveDocument>("rebrickable_parts_inventory");
        _meta = database.GetCollection<ArchiveMetaDocument>("archive_meta");
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<RebrickablePartInventory> parts)
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

    public async Task<IEnumerable<RebrickablePartInventory>> SearchAsync(string query, int limit)
    {
        var escaped = Regex.Escape(query);
        var regex = new BsonRegularExpression(escaped, "i");
        var filter = Builders<PartInventoryArchiveDocument>.Filter.Or(
            Builders<PartInventoryArchiveDocument>.Filter.Regex(x => x.PartNum, regex),
            Builders<PartInventoryArchiveDocument>.Filter.Regex(x => x.ImageUrl, regex));
        var docs = await _collection.Find(filter).Limit(limit).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<RebrickablePartInventory?> GetByPartNumAsync(string partNum)
    {
        var doc = await _collection.Find(x => x.PartNum == partNum).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    private static RebrickablePartInventory ToModel(PartInventoryArchiveDocument doc) => new()
    {
        PartNum = doc.PartNum,
        ImgUrl = doc.ImageUrl,
    };

    private static PartInventoryArchiveDocument ToDocument(RebrickablePartInventory model) => new()
    {
        PartNum = model.PartNum,
        ImageUrl = model.ImgUrl,
    };
}
