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

    public async Task<Baseplate> CreateAsync(Baseplate baseplate)
    {
        var doc = ToDocument(baseplate);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _baseplates.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _baseplates.DeleteOneAsync(d => d.Id == id);
    }

    private static Baseplate ToModel(BaseplateDocument doc) => new()
    {
        Id = doc.Id,
        PartNum = doc.PartNum,
        Name = doc.Name,
        WidthStuds = doc.WidthStuds,
        DepthStuds = doc.DepthStuds,
        LegoColorId = doc.LegoColorId,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static BaseplateDocument ToDocument(Baseplate model) => new()
    {
        Id = model.Id,
        PartNum = model.PartNum,
        Name = model.Name,
        WidthStuds = model.WidthStuds,
        DepthStuds = model.DepthStuds,
        LegoColorId = model.LegoColorId,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
