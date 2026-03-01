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
        var colors = await _db.GetCollection<ColorDocument>("rebrickable_colors").Find(_ => true).ToListAsync();
        var sets = await _db.GetCollection<SetArchiveDocument>("rebrickable_sets").Find(_ => true).ToListAsync();
        var parts = await _db.GetCollection<PartArchiveDocument>("rebrickable_parts").Find(_ => true).ToListAsync();
        var partCategories = await _db.GetCollection<PartCategoryDocument>("rebrickable_part_categories").Find(_ => true).ToListAsync();
        var partsInventory = await _db.GetCollection<PartInventoryArchiveDocument>("rebrickable_parts_inventory").Find(_ => true).ToListAsync();
        var archiveMeta = await _db.GetCollection<ArchiveMetaDocument>("archive_meta").Find(_ => true).ToListAsync();
        var images = await _db.GetCollection<ImageDocument>("set_images").Find(_ => true).ToListAsync();

        var backup = new
        {
            version = 2,
            exportedAt = DateTimeOffset.UtcNow,
            legoSets,
            bulkPieces,
            boxes,
            drawerContainers = containers,
            drawers,
            tableTemplates,
            rooms,
            baseplates,
            colors,
            sets,
            parts,
            partCategories,
            partsInventory,
            archiveMeta,
            images,
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

        // User data
        await _db.GetCollection<LegoSetDocument>("legosets").DeleteManyAsync(_ => true);
        await _db.GetCollection<BulkPieceDocument>("bulkpieces").DeleteManyAsync(_ => true);
        await _db.GetCollection<BoxDocument>("boxes").DeleteManyAsync(_ => true);
        await _db.GetCollection<DrawerContainerDocument>("drawercontainers").DeleteManyAsync(_ => true);
        await _db.GetCollection<DrawerDocument>("drawers").DeleteManyAsync(_ => true);
        await _db.GetCollection<TableTemplateDocument>("table_templates").DeleteManyAsync(_ => true);
        await _db.GetCollection<RoomDocument>("rooms").DeleteManyAsync(_ => true);
        await _db.GetCollection<BaseplateDocument>("baseplates").DeleteManyAsync(_ => true);
        // Rebrickable catalog
        await _db.GetCollection<ColorDocument>("rebrickable_colors").DeleteManyAsync(_ => true);
        await _db.GetCollection<SetArchiveDocument>("rebrickable_sets").DeleteManyAsync(_ => true);
        await _db.GetCollection<PartArchiveDocument>("rebrickable_parts").DeleteManyAsync(_ => true);
        await _db.GetCollection<PartCategoryDocument>("rebrickable_part_categories").DeleteManyAsync(_ => true);
        await _db.GetCollection<PartInventoryArchiveDocument>("rebrickable_parts_inventory").DeleteManyAsync(_ => true);
        await _db.GetCollection<ArchiveMetaDocument>("archive_meta").DeleteManyAsync(_ => true);
        await _db.GetCollection<ImageDocument>("set_images").DeleteManyAsync(_ => true);

        // Restore user data
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
        // Restore Rebrickable catalog
        if (backup.Colors?.Count > 0)
            await _db.GetCollection<ColorDocument>("rebrickable_colors").InsertManyAsync(backup.Colors);
        if (backup.Sets?.Count > 0)
            await _db.GetCollection<SetArchiveDocument>("rebrickable_sets").InsertManyAsync(backup.Sets);
        if (backup.Parts?.Count > 0)
            await _db.GetCollection<PartArchiveDocument>("rebrickable_parts").InsertManyAsync(backup.Parts);
        if (backup.PartCategories?.Count > 0)
            await _db.GetCollection<PartCategoryDocument>("rebrickable_part_categories").InsertManyAsync(backup.PartCategories);
        if (backup.PartsInventory?.Count > 0)
            await _db.GetCollection<PartInventoryArchiveDocument>("rebrickable_parts_inventory").InsertManyAsync(backup.PartsInventory);
        if (backup.ArchiveMeta?.Count > 0)
            await _db.GetCollection<ArchiveMetaDocument>("archive_meta").InsertManyAsync(backup.ArchiveMeta);
        if (backup.Images?.Count > 0)
            await _db.GetCollection<ImageDocument>("set_images").InsertManyAsync(backup.Images);
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
        public List<ColorDocument>? Colors { get; set; }
        public List<SetArchiveDocument>? Sets { get; set; }
        public List<PartArchiveDocument>? Parts { get; set; }
        public List<PartCategoryDocument>? PartCategories { get; set; }
        public List<PartInventoryArchiveDocument>? PartsInventory { get; set; }
        public List<ArchiveMetaDocument>? ArchiveMeta { get; set; }
        public List<ImageDocument>? Images { get; set; }
    }
}
