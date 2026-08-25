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

    public async Task<Image?> GetAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null)
    {
        var key = BuildKey(referenceNumber, referenceType, colorId);
        var doc = await _collection.Find(x => x.ReferenceNumber == key && x.ReferenceType == referenceType.ToString()).FirstOrDefaultAsync();
        if (doc is null) return null;
        return new Image
        {
            ReferenceNumber = doc.ReferenceNumber,
            ColorId = doc.ColorId,
            Data = doc.Data,
            ContentType = doc.ContentType,
            DownloadedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.DownloadedAt, DateTimeKind.Utc)),
            ReferenceType = Enum.Parse<ImageReferenceType>(doc.ReferenceType),
        };
    }

    public async Task StoreAsync(Image image)
    {
        var key = BuildKey(image.ReferenceNumber, image.ReferenceType, image.ColorId);
        var doc = new ImageDocument
        {
            ReferenceNumber = key,
            ColorId = image.ColorId,
            Data = image.Data,
            ContentType = image.ContentType,
            DownloadedAt = image.DownloadedAt.UtcDateTime,
            ReferenceType = image.ReferenceType.ToString(),
        };
        await _collection.ReplaceOneAsync(
            x => x.ReferenceNumber == key && x.ReferenceType == image.ReferenceType.ToString(),
            doc,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null)
    {
        var key = BuildKey(referenceNumber, referenceType, colorId);
        await _collection.DeleteOneAsync(x => x.ReferenceNumber == key && x.ReferenceType == referenceType.ToString());
    }

    /// <summary>
    /// Builds the storage key for an image. Part images are addressed per (partNum, colorId),
    /// so the key embeds the color; all other types are keyed by their reference only.
    /// </summary>
    private static string BuildKey(string referenceNumber, ImageReferenceType referenceType, int? colorId)
    {
        if (referenceType == ImageReferenceType.Part)
            return $"{referenceNumber}:{colorId ?? 0}";
        return referenceNumber;
    }
}
