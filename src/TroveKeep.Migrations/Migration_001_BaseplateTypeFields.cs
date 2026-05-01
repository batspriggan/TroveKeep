using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Backfills Type=0 (Standard), ImageCached=false, LinkedSetId=null on existing
/// baseplate documents that pre-date the typed baseplate system.
/// </summary>
public class Migration_001_BaseplateTypeFields : IMigration
{
    public int VersionFrom => 0;
    public int VersionTo => 1;

    public async Task RunAsync(IMongoDatabase database)
    {
        var baseplates = database.GetCollection<BsonDocument>("baseplates");

        var filter = Builders<BsonDocument>.Filter.Exists("Type", false);
        var update = Builders<BsonDocument>.Update
            .Set("Type", 0)             // BaseplateType.Standard
            .Set("ImageCached", false)
            .Set("LinkedSetId", BsonNull.Value);

        await baseplates.UpdateManyAsync(filter, update);
    }
}
