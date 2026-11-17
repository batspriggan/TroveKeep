using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

/// <summary>
/// Repository of the global sticker image library (collection <c>sticker_images</c>).
///
/// <para>
/// Flat like <see cref="ImageRepository"/>: an image belongs to no collection, so there is no
/// <c>ICollectionScope</c> filter here. Authorization is orthogonal and lives in
/// <c>CollectionWriteAnywhereFilter</c>.
/// </para>
/// </summary>
public class StickerImageRepository : IStickerImageRepository
{
    private readonly IMongoCollection<StickerImageDocument> _images;

    public StickerImageRepository(IMongoDatabase database)
    {
        _images = database.GetCollection<StickerImageDocument>("sticker_images");
    }

    public async Task<IEnumerable<StickerImage>> GetAllAsync()
    {
        var docs = await _images.Find(_ => true).ToListAsync();
        return docs
            .Select(ToModel)
            // Deterministic order so the library does not depend on MongoDB's natural order.
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Id);
    }

    public async Task<StickerImage?> GetByIdAsync(Guid id)
    {
        var doc = await _images.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<StickerImage> CreateAsync(StickerImage image)
    {
        var doc = ToDocument(image);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _images.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<StickerImage?> UpdateAsync(StickerImage image)
    {
        var existing = await _images.Find(x => x.Id == image.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(image);
        doc.Id = image.Id;
        doc.CreatedAt = existing.CreatedAt;
        doc.ImageCached = existing.ImageCached;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _images.ReplaceOneAsync(
            x => x.Id == image.Id && x.Version == image.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException(
                $"Sticker image {image.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _images.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<StickerImage?> UpdateImageCachedAsync(Guid id, bool cached)
    {
        var update = Builders<StickerImageDocument>.Update
            .Set(x => x.ImageCached, cached)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Inc(x => x.Version, 1);

        var doc = await _images.FindOneAndUpdateAsync(x => x.Id == id, update,
            new FindOneAndUpdateOptions<StickerImageDocument> { ReturnDocument = ReturnDocument.After });
        return doc is null ? null : ToModel(doc);
    }

    private static StickerImage ToModel(StickerImageDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static StickerImageDocument ToDocument(StickerImage model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        ImageCached = model.ImageCached,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
