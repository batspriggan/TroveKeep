using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class SetImageRepository : ISetImageRepository
{
    private readonly IMongoCollection<SetImageDocument> _collection;

    public SetImageRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SetImageDocument>("set_images");
    }

    public async Task<SetImage?> GetAsync(string setNum)
    {
        var doc = await _collection.Find(x => x.SetNum == setNum).FirstOrDefaultAsync();
        if (doc is null) return null;
        return new SetImage
        {
            SetNum = doc.SetNum,
            Data = doc.Data,
            ContentType = doc.ContentType,
            DownloadedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.DownloadedAt, DateTimeKind.Utc)),
        };
    }

    public async Task StoreAsync(SetImage image)
    {
        var doc = new SetImageDocument
        {
            SetNum = image.SetNum,
            Data = image.Data,
            ContentType = image.ContentType,
            DownloadedAt = image.DownloadedAt.UtcDateTime,
        };
        await _collection.ReplaceOneAsync(
            x => x.SetNum == image.SetNum,
            doc,
            new ReplaceOptions { IsUpsert = true });
    }
}
