using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class ImageRepository : IImageRepository
{
    private readonly IMongoCollection<ImageDocument> _collection;

    public ImageRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ImageDocument>("set_images");
    }

    public async Task<Image?> GetAsync(string referenceNumber, ImageReferenceType referenceType)
    {
        var doc = await _collection.Find(x => x.ReferenceNumber == referenceNumber && x.ReferenceType == referenceType.ToString()).FirstOrDefaultAsync();
        if (doc is null) return null;
        return new Image
        {
            ReferenceNumber = doc.ReferenceNumber,
            Data = doc.Data,
            ContentType = doc.ContentType,
            DownloadedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.DownloadedAt, DateTimeKind.Utc)),
            ReferenceType = Enum.Parse<ImageReferenceType>(doc.ReferenceType),
        };
    }

    public async Task StoreAsync(Image image)
    {
        var doc = new ImageDocument
        {
            ReferenceNumber = image.ReferenceNumber,
            Data = image.Data,
            ContentType = image.ContentType,
            DownloadedAt = image.DownloadedAt.UtcDateTime,
            ReferenceType = image.ReferenceType.ToString(), 
        };
        await _collection.ReplaceOneAsync(
            x => x.ReferenceNumber == image.ReferenceNumber && x.ReferenceType == image.ReferenceType.ToString(),
            doc,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType)
    {
        await _collection.DeleteOneAsync(x => x.ReferenceNumber == referenceNumber && x.ReferenceType == referenceType.ToString());
    }
}
