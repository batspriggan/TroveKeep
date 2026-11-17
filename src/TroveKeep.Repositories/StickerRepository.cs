using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

/// <summary>
/// Repository of the global sticker library (collection <c>stickers</c>).
///
/// <para>
/// Flat like <see cref="ImageRepository"/>: a sticker belongs to no collection, so there is no
/// <c>ICollectionScope</c> filter here. Authorization is orthogonal and lives in
/// <c>CollectionWriteAnywhereFilter</c>.
/// </para>
/// </summary>
public class StickerRepository : IStickerRepository
{
    private readonly IMongoCollection<StickerDocument> _stickers;

    public StickerRepository(IMongoDatabase database)
    {
        _stickers = database.GetCollection<StickerDocument>("stickers");
    }

    public async Task<IEnumerable<Sticker>> GetAllAsync()
    {
        var docs = await _stickers.Find(_ => true).ToListAsync();
        return docs
            .Select(ToModel)
            // Deterministic order so the library and the generated sheet do not depend on
            // MongoDB's natural order.
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.WidthStuds)
            .ThenBy(s => s.DepthStuds)
            .ThenBy(s => s.Id);
    }

    public async Task<Sticker?> GetByIdAsync(Guid id)
    {
        var doc = await _stickers.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Sticker> CreateAsync(Sticker sticker)
    {
        var doc = ToDocument(sticker);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _stickers.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Sticker?> UpdateAsync(Sticker sticker)
    {
        var existing = await _stickers.Find(x => x.Id == sticker.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(sticker);
        doc.Id = sticker.Id;
        doc.CreatedAt = existing.CreatedAt;
        doc.ImageCached = existing.ImageCached;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _stickers.ReplaceOneAsync(
            x => x.Id == sticker.Id && x.Version == sticker.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException(
                $"Sticker {sticker.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _stickers.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<Sticker?> UpdateImageCachedAsync(Guid id, bool cached)
    {
        var update = Builders<StickerDocument>.Update
            .Set(x => x.ImageCached, cached)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Inc(x => x.Version, 1);

        var doc = await _stickers.FindOneAndUpdateAsync(x => x.Id == id, update,
            new FindOneAndUpdateOptions<StickerDocument> { ReturnDocument = ReturnDocument.After });
        return doc is null ? null : ToModel(doc);
    }

    private static Sticker ToModel(StickerDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        WidthStuds = doc.WidthStuds,
        DepthStuds = doc.DepthStuds,
        ImageCached = doc.ImageCached,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static StickerDocument ToDocument(Sticker model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        WidthStuds = model.WidthStuds,
        DepthStuds = model.DepthStuds,
        ImageCached = model.ImageCached,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
