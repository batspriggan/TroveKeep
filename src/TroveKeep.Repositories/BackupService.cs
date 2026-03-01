using System.Text.Json;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BackupService : IBackupService
{
    private readonly IMongoDatabase _db;

    public BackupService(IMongoDatabase db) => _db = db;

    public async Task<(byte[] Data, string FileName)> ExportAsync()
    {
        var legoSets = await _db.GetCollection<LegoSetDocument>("legosets").Find(_ => true).ToListAsync();
        var bulkPieces = await _db.GetCollection<BulkPieceDocument>("bulkpieces").Find(_ => true).ToListAsync();
        var boxes = await _db.GetCollection<BoxDocument>("boxes").Find(_ => true).ToListAsync();
        var containers = await _db.GetCollection<DrawerContainerDocument>("drawercontainers").Find(_ => true).ToListAsync();
        var drawers = await _db.GetCollection<DrawerDocument>("drawers").Find(_ => true).ToListAsync();
        var tableTemplates = await _db.GetCollection<TableTemplateDocument>("table_templates").Find(_ => true).ToListAsync();
        var rooms = await _db.GetCollection<RoomDocument>("rooms").Find(_ => true).ToListAsync();
        var baseplates = await _db.GetCollection<BaseplateDocument>("baseplates").Find(_ => true).ToListAsync();

        var backup = new
        {
            version = 1,
            exportedAt = DateTimeOffset.UtcNow,
            legoSets,
            bulkPieces,
            boxes,
            drawerContainers = containers,
            drawers,
            tableTemplates,
            rooms,
            baseplates,
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var data = JsonSerializer.SerializeToUtf8Bytes(backup, options);
        var fileName = $"trovekeep-backup-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return (data, fileName);
    }

    public async Task ImportAsync(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var backup = await JsonSerializer.DeserializeAsync<BackupPayload>(stream, options)
            ?? throw new InvalidOperationException("Invalid backup file: could not parse JSON");

        await _db.GetCollection<LegoSetDocument>("legosets").DeleteManyAsync(_ => true);
        await _db.GetCollection<BulkPieceDocument>("bulkpieces").DeleteManyAsync(_ => true);
        await _db.GetCollection<BoxDocument>("boxes").DeleteManyAsync(_ => true);
        await _db.GetCollection<DrawerContainerDocument>("drawercontainers").DeleteManyAsync(_ => true);
        await _db.GetCollection<DrawerDocument>("drawers").DeleteManyAsync(_ => true);
        await _db.GetCollection<TableTemplateDocument>("table_templates").DeleteManyAsync(_ => true);
        await _db.GetCollection<RoomDocument>("rooms").DeleteManyAsync(_ => true);
        await _db.GetCollection<BaseplateDocument>("baseplates").DeleteManyAsync(_ => true);

        if (backup.Boxes?.Count > 0)
            await _db.GetCollection<BoxDocument>("boxes").InsertManyAsync(backup.Boxes);
        if (backup.DrawerContainers?.Count > 0)
            await _db.GetCollection<DrawerContainerDocument>("drawercontainers").InsertManyAsync(backup.DrawerContainers);
        if (backup.Drawers?.Count > 0)
            await _db.GetCollection<DrawerDocument>("drawers").InsertManyAsync(backup.Drawers);
        if (backup.LegoSets?.Count > 0)
            await _db.GetCollection<LegoSetDocument>("legosets").InsertManyAsync(backup.LegoSets);
        if (backup.BulkPieces?.Count > 0)
            await _db.GetCollection<BulkPieceDocument>("bulkpieces").InsertManyAsync(backup.BulkPieces);
        if (backup.TableTemplates?.Count > 0)
            await _db.GetCollection<TableTemplateDocument>("table_templates").InsertManyAsync(backup.TableTemplates);
        if (backup.Rooms?.Count > 0)
            await _db.GetCollection<RoomDocument>("rooms").InsertManyAsync(backup.Rooms);
        if (backup.Baseplates?.Count > 0)
            await _db.GetCollection<BaseplateDocument>("baseplates").InsertManyAsync(backup.Baseplates);
    }

    private class BackupPayload
    {
        public List<LegoSetDocument>? LegoSets { get; set; }
        public List<BulkPieceDocument>? BulkPieces { get; set; }
        public List<BoxDocument>? Boxes { get; set; }
        public List<DrawerContainerDocument>? DrawerContainers { get; set; }
        public List<DrawerDocument>? Drawers { get; set; }
        public List<TableTemplateDocument>? TableTemplates { get; set; }
        public List<RoomDocument>? Rooms { get; set; }
        public List<BaseplateDocument>? Baseplates { get; set; }
    }
}
