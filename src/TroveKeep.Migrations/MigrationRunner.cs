using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

public class MigrationRunner
{
    private readonly IMongoDatabase _db;
    private readonly IReadOnlyList<IMigration> _migrations;

    public MigrationRunner(IMongoDatabase db)
    {
        _db = db;
        _migrations = [
            new Migration_001_BaseplateTypeFields(),
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
}
