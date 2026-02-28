using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly IMongoCollection<RoomDocument> _rooms;

    public RoomRepository(IMongoDatabase database)
    {
        _rooms = database.GetCollection<RoomDocument>("rooms");
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        var docs = await _rooms.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        var doc = await _rooms.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Room> CreateAsync(Room room)
    {
        var doc = ToDocument(room);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _rooms.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Room?> UpdateAsync(Room room)
    {
        var existing = await _rooms.Find(x => x.Id == room.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(room);
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        // Preserve existing layout
        doc.Layout = existing.Layout;
        await _rooms.ReplaceOneAsync(x => x.Id == room.Id, doc);
        return ToModel(doc);
    }

    public async Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout)
    {
        var layoutDocs = layout.Select(p => new PlacedTableDocument
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
        }).ToList();

        var update = Builders<RoomDocument>.Update
            .Set(r => r.Layout, layoutDocs)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await _rooms.FindOneAndUpdateAsync(
            x => x.Id == id,
            update,
            new FindOneAndUpdateOptions<RoomDocument> { ReturnDocument = ReturnDocument.After });

        return result is null ? null : ToModel(result);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _rooms.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    private static Room ToModel(RoomDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        WidthCm = doc.WidthCm,
        DepthCm = doc.DepthCm,
        Layout = doc.Layout.Select(p => new PlacedTable
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
        }).ToList(),
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static RoomDocument ToDocument(Room model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        WidthCm = model.WidthCm,
        DepthCm = model.DepthCm,
        Layout = model.Layout.Select(p => new PlacedTableDocument
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
        }).ToList(),
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
