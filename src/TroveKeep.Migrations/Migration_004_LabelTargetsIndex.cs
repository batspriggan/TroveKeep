using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Creates the label_targets collection index for storage lookups (issue: neutral QR codes).
/// The collection itself is created lazily on first write; this migration only adds the index
/// used when re-keying label associations after a drawer move.
/// </summary>
public class Migration_004_LabelTargetsIndex : IMigration
{
    public int VersionFrom => 3;
    public int VersionTo => 4;
    public string Description =>
        "Add label_targets storage index for the neutral QR code table.";

    public async Task RunAsync(IMongoDatabase database)
    {
        var labels = database.GetCollection<BsonDocument>("label_targets");
        await labels.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("StorageId").Ascending("StoragePosition")));
    }
}
