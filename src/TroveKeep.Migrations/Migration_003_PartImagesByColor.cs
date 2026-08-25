using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Re-keys part images to be per-color (issue #2).
///
/// Before this migration, part images were stored under <c>_id = partNum</c> and carried no
/// color. Going forward each part image is keyed by <c>"{partNum}:{colorId}"</c> (with the
/// <c>ColorId</c> field set), so a piece can have a different image per color. This migration
/// rewrites the existing color-less part images to color 0 (they were all imported as
/// color-0 / grayscale renders) by re-creating each document with the new key.
/// </summary>
public class Migration_003_PartImagesByColor : IMigration
{
    public int VersionFrom => 2;
    public int VersionTo => 3;
    public string Description =>
        "Re-key part images by (partNum, colorId): move color-less part images to color 0.";

    public async Task RunAsync(IMongoDatabase database)
    {
        var images = database.GetCollection<BsonDocument>("set_images");

        // Old part images: ReferenceType "Part" and no ColorId yet.
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("ReferenceType", "Part"),
            Builders<BsonDocument>.Filter.Exists("ColorId", false));

        var docs = await images.Find(filter).ToListAsync();
        foreach (var doc in docs)
        {
            var oldKey = doc["_id"].AsString;
            var newKey = $"{oldKey}:0";
            doc["_id"] = newKey;
            doc["ColorId"] = 0;

            // Don't overwrite an image that already exists under the new key.
            var existing = await images.Find(Builders<BsonDocument>.Filter.Eq("_id", newKey)).FirstOrDefaultAsync();
            if (existing is not null) continue;

            await images.InsertOneAsync(doc);
            await images.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", oldKey));
        }
    }
}
