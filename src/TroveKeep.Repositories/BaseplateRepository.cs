using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BaseplateRepository : IBaseplateRepository
{
    private readonly IMongoCollection<BaseplateDocument> _baseplates;

    public BaseplateRepository(IMongoDatabase database)
    {
        _baseplates = database.GetCollection<BaseplateDocument>("baseplates");
    }

    public async Task<IEnumerable<Baseplate>> GetAllAsync()
    {
        var docs = await _baseplates.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Baseplate?> GetByIdAsync(Guid id)
    {
        var doc = await _baseplates.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Baseplate> CreateAsync(Baseplate baseplate)
    {
        var doc = ToDocument(baseplate);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _baseplates.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task UpdateImageCachedAsync(Guid id)
    {
        var update = Builders<BaseplateDocument>.Update
            .Set(d => d.ImageCached, true)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _baseplates.UpdateOneAsync(d => d.Id == id, update);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _baseplates.DeleteOneAsync(d => d.Id == id);
    }

    public async Task DeleteByLinkedSetIdAsync(Guid setId)
    {
        await _baseplates.DeleteManyAsync(d => d.LinkedSetId == setId);
    }

    private static Baseplate ToModel(BaseplateDocument doc) => new()
    {
        Id = doc.Id,
        Type = doc.Type,
        PartNum = doc.PartNum,
        Name = doc.Name,
        WidthStuds = doc.WidthStuds,
        DepthStuds = doc.DepthStuds,
        LegoColorId = doc.LegoColorId,
        ImageCached = doc.ImageCached,
        LinkedSetId = doc.LinkedSetId,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static BaseplateDocument ToDocument(Baseplate model) => new()
    {
        Id = model.Id,
        Type = model.Type,
        PartNum = model.PartNum,
        Name = model.Name,
        WidthStuds = model.WidthStuds,
        DepthStuds = model.DepthStuds,
        LegoColorId = model.LegoColorId,
        ImageCached = model.ImageCached,
        LinkedSetId = model.LinkedSetId,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
