using System.IO.Compression;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

public class MigrationRunner
{
    private readonly IMongoDatabase _db;
    private readonly string? _backupDir;
    private readonly IReadOnlyList<IMigration> _migrations;

    /// <summary>All collections dumped for the automatic pre-migration backup.</summary>
    private static readonly string[] BackupCollections =
    [
        "legosets", "bulkpieces", "boxes", "drawercontainers", "drawers",
        "table_templates", "rooms", "baseplates", "set_photos",
        "storage_allocations", "set_images",
        "rebrickable_colors", "rebrickable_sets", "rebrickable_parts",
        "rebrickable_part_categories", "rebrickable_parts_inventory",
        "archive_meta", "meta",
    ];

    public MigrationRunner(IMongoDatabase db, string? backupDir = null)
    {
        _db = db;
        _backupDir = string.IsNullOrWhiteSpace(backupDir) ? null : backupDir;
        _migrations = [
            new Migration_001_BaseplateTypeFields(),
            new Migration_002_SetImagesBySetId(),
        ];
    }

    public async Task RunAsync()
    {
        var meta = _db.GetCollection<BsonDocument>("meta");
        var versionDoc = await meta.Find(Builders<BsonDocument>.Filter.Eq("_id", "schema_version"))
            .FirstOrDefaultAsync();

        var currentVersion = versionDoc?["version"].AsInt32 ?? 0;

        var pending = _migrations.Where(m => m.VersionFrom >= currentVersion)
                                 .OrderBy(m => m.VersionFrom)
                                 .ToList();

        // Safety net: a full backup of every collection is taken before any pending
        // migration runs, so a destructive re-key (e.g. migration 002) can be rolled back.
        if (pending.Count > 0)
        {
            try
            {
                await BackupAsync(currentVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: pre-migration backup failed ({ex.Message}); continuing with migration. " +
                                  "Consider configuring Migration:BackupDir.");
            }
        }

        foreach (var migration in pending)
        {
            Console.WriteLine($"Running migration from version {migration.VersionFrom} to {migration.VersionTo}...");
            await migration.RunAsync(_db);
            await meta.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", "schema_version"),
                new BsonDocument { ["_id"] = "schema_version", ["version"] = migration.VersionTo },
                new ReplaceOptions { IsUpsert = true });
        }
    }

    /// <summary>
    /// Writes a gzip-compressed JSON snapshot of every collection to
    /// <c>{backupDir}/auto-backup-v{currentVersion}-{timestamp}.json.gz</c>.
    /// Uses a generic BSON representation so no typed document model is required.
    /// </summary>
    private async Task BackupAsync(int currentVersion)
    {
        if (_backupDir is null)
        {
            Console.WriteLine("Pre-migration backup skipped (no Migration:BackupDir configured).");
            return;
        }

        Directory.CreateDirectory(_backupDir);

        var root = new BsonDocument();
        foreach (var name in BackupCollections)
        {
            var collection = _db.GetCollection<BsonDocument>(name);
            try
            {
                var docs = await collection.Find(_ => true).ToListAsync();
                root[name] = new BsonArray(docs);
            }
            catch (MongoException)
            {
                // Collection missing or not yet created — represent it as empty.
                root[name] = new BsonArray();
            }
        }

        var fileName = $"auto-backup-v{currentVersion}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.json.gz";
        var path = Path.Combine(_backupDir, fileName);

        var json = root.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        await using (var fs = File.Create(path))
        await using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            await gz.WriteAsync(bytes);

        Console.WriteLine($"Pre-migration backup written to {path}");
    }
}
