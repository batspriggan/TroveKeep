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
            new Migration_003_PartImagesByColor(),
            new Migration_004_LabelTargetsIndex(),
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

        // Fail-fast safety net: a full backup of every collection must succeed before any
        // pending migration runs, so a destructive migration (e.g. #002) can always be
        // rolled back. If the backup is not configured or fails, we abort without touching
        // the database. Likewise, if a migration throws, we stop immediately: the failed
        // migration is never marked as applied (schema_version is unchanged) and NO
        // subsequent migration runs.
        if (pending.Count > 0)
        {
            await BackupAsync(currentVersion);
        }

        foreach (var migration in pending)
        {
            Console.WriteLine($"Running migration from version {migration.VersionFrom} to {migration.VersionTo}...");
            await migration.RunAsync(_db);
            await meta.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", "schema_version"),
                new BsonDocument { ["_id"] = "schema_version", ["version"] = migration.VersionTo },
                new ReplaceOptions { IsUpsert = true });
            Console.WriteLine($"Migration {migration.VersionFrom} -> {migration.VersionTo} completed.");
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
            throw new InvalidOperationException(
                "Pre-migration backup required but Migration:BackupDir is not configured. " +
                "Refusing to run migrations without a backup. Set Migration__BackupDir (env) " +
                "or Migration:BackupDir in appsettings.");

        Directory.CreateDirectory(_backupDir);

        var root = new BsonDocument();
        foreach (var name in BackupCollections)
        {
            // Reading a missing collection returns an empty list (Mongo creates it lazily),
            // so no try/catch around reads: any real read error must propagate (fail-fast).
            var collection = _db.GetCollection<BsonDocument>(name);
            var docs = await collection.Find(_ => true).ToListAsync();
            root[name] = new BsonArray(docs);
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
