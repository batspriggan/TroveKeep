using System.IO.Compression;
using System.Text.Json;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class RoomExportService : IRoomExportService
{
    private readonly IMongoDatabase _db;
    private readonly ICollectionScope _scope;

    public RoomExportService(IMongoDatabase db, ICollectionScope? scope = null)
    {
        _db = db;
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<(byte[] Data, string FileName)> ExportRoomAsync(Guid roomId)
    {
        // This service talks to IMongoDatabase directly (it has no repository dependency), so the
        // collection filter has to be applied here too: a room outside the caller's scope must not
        // be exported.
        var roomFilter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, roomId));
        var room = await _db.GetCollection<RoomDocument>("rooms")
            .Find(roomFilter)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Room {roomId} not found");

        var templateIds = room.Layout.Select(p => p.TemplateId).Distinct().ToHashSet();

        var templateFilter = _scope.ScopedTo<TableTemplateDocument>(d => d.CollectionId,
            Builders<TableTemplateDocument>.Filter.In(x => x.Id, templateIds));
        var allTemplates = await _db.GetCollection<TableTemplateDocument>("table_templates")
            .Find(templateFilter)
            .ToListAsync();

        var roomData = new RoomData(
            room.Name,
            room.WidthCm,
            room.DepthCm,
            room.Layout.Select(p => new PlacedData(p.InstanceId, p.TemplateId, p.XCm, p.YCm, p.Rotation)).ToList()
        );

        var templateData = allTemplates.Select(t =>
            new TemplateData(t.Id, t.Description, t.WidthCm, t.DepthCm, t.Color)).ToList();

        var roomBytes = JsonSerializer.SerializeToUtf8Bytes(roomData, ExportOptions);
        var templateBytes = JsonSerializer.SerializeToUtf8Bytes(templateData, ExportOptions);

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var roomEntry = archive.CreateEntry("room.json");
            using (var s = roomEntry.Open()) await s.WriteAsync(roomBytes);

            var tplEntry = archive.CreateEntry("templates.json");
            using (var s = tplEntry.Open()) await s.WriteAsync(templateBytes);
        }

        var slug = room.Name.ToLowerInvariant().Replace(' ', '-');
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"room-{slug}-{date}.zip";

        return (ms.ToArray(), fileName);
    }

    public async Task<Room> ImportRoomAsync(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var roomEntry = archive.GetEntry("room.json")
            ?? throw new InvalidOperationException("Invalid ZIP: missing room.json");
        var tplEntry = archive.GetEntry("templates.json")
            ?? throw new InvalidOperationException("Invalid ZIP: missing templates.json");

        RoomData roomData;
        List<TemplateData> templateData;

        try
        {
            using var rs = roomEntry.Open();
            roomData = await JsonSerializer.DeserializeAsync<RoomData>(rs, ImportOptions)
                ?? throw new InvalidOperationException("Could not parse room.json");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid room.json: {ex.Message}");
        }

        try
        {
            using var ts = tplEntry.Open();
            templateData = await JsonSerializer.DeserializeAsync<List<TemplateData>>(ts, ImportOptions)
                ?? throw new InvalidOperationException("Could not parse templates.json");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid templates.json: {ex.Message}");
        }

        var templatesCollection = _db.GetCollection<TableTemplateDocument>("table_templates");
        var collectionId = _scope.ResolveCreateCollectionId(Guid.Empty);
        foreach (var t in templateData)
        {
            var existing = await templatesCollection.Find(x => x.Id == t.Id).FirstOrDefaultAsync();
            if (existing is null)
            {
                await templatesCollection.InsertOneAsync(new TableTemplateDocument
                {
                    Id = t.Id,
                    CollectionId = collectionId,
                    Description = t.Description,
                    WidthCm = t.WidthCm,
                    DepthCm = t.DepthCm,
                    Color = t.Color,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        var newRoom = new RoomDocument
        {
            Id = Guid.NewGuid(),
            CollectionId = collectionId,
            Name = roomData.Name,
            WidthCm = roomData.WidthCm,
            DepthCm = roomData.DepthCm,
            Layout = roomData.Layout.Select(p => new PlacedTableDocument
            {
                InstanceId = p.InstanceId,
                TemplateId = p.TemplateId,
                XCm = p.XCm,
                YCm = p.YCm,
                Rotation = p.Rotation,
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _db.GetCollection<RoomDocument>("rooms").InsertOneAsync(newRoom);

        return new Room
        {
            Id = newRoom.Id,
            CollectionId = newRoom.CollectionId,
            Name = newRoom.Name,
            WidthCm = newRoom.WidthCm,
            DepthCm = newRoom.DepthCm,
            Layout = newRoom.Layout.Select(p => new PlacedTable
            {
                InstanceId = p.InstanceId,
                TemplateId = p.TemplateId,
                XCm = p.XCm,
                YCm = p.YCm,
                Rotation = p.Rotation,
            }).ToList(),
            CreatedAt = newRoom.CreatedAt,
            UpdatedAt = newRoom.UpdatedAt,
        };
    }

    private record RoomData(string Name, int WidthCm, int DepthCm, List<PlacedData> Layout);
    private record PlacedData(Guid InstanceId, Guid TemplateId, double XCm, double YCm, int Rotation = 0);
    private record TemplateData(Guid Id, string Description, int WidthCm, int DepthCm, string Color);
}
