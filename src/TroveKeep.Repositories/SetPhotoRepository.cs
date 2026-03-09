using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class SetPhotoRepository : ISetPhotoRepository
{
    private readonly IMongoCollection<SetPhotoDocument> _collection;

    public SetPhotoRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SetPhotoDocument>("set_photos");
    }

    public async Task<IEnumerable<SetPhoto>> GetMetadataBySetIdAsync(Guid setId)
    {
        var projection = Builders<SetPhotoDocument>.Projection.Exclude(d => d.Data);
        var docs = await _collection
            .Find(x => x.SetId == setId)
            .Project<SetPhotoDocument>(projection)
            .ToListAsync();
        return docs.Select(ToMetadata);
    }

    public async Task<SetPhoto?> GetByIdAsync(Guid id)
    {
        var doc = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Dictionary<Guid, int>> GetCountsBySetIdsAsync(IEnumerable<Guid> setIds)
    {
        var idList = setIds.ToList();
        if (idList.Count == 0) return [];

        var projection = Builders<SetPhotoDocument>.Projection.Exclude(d => d.Data);
        var docs = await _collection
            .Find(x => idList.Contains(x.SetId))
            .Project<SetPhotoDocument>(projection)
            .ToListAsync();

        return docs
            .GroupBy(d => d.SetId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Guid> StoreAsync(Guid setId, byte[] data, string contentType)
    {
        var doc = new SetPhotoDocument
        {
            Id = Guid.NewGuid(),
            SetId = setId,
            Data = data,
            ContentType = contentType,
            UploadedAt = DateTime.UtcNow,
        };
        await _collection.InsertOneAsync(doc);
        return doc.Id;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task DeleteBySetIdAsync(Guid setId)
    {
        await _collection.DeleteManyAsync(x => x.SetId == setId);
    }

    private static SetPhoto ToMetadata(SetPhotoDocument doc) => new()
    {
        Id = doc.Id,
        SetId = doc.SetId,
        ContentType = doc.ContentType,
        UploadedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UploadedAt, DateTimeKind.Utc)),
    };

    private static SetPhoto ToModel(SetPhotoDocument doc) => new()
    {
        Id = doc.Id,
        SetId = doc.SetId,
        Data = doc.Data,
        ContentType = doc.ContentType,
        UploadedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UploadedAt, DateTimeKind.Utc)),
    };
}
