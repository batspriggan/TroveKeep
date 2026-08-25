using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Re-keys existing set images from the set number to the set entity id.
///
/// Before this migration, set images in <c>set_images</c> were keyed by
/// <c>ReferenceNumber = SetNumber</c> (and <c>ReferenceType = "Set"</c>). That was a
/// problem for MOCs (SetNumber is empty and shared by every MOC, so their images
/// collided under the same key). Going forward both sets and MOCs use the set id as
/// the unique image key, so existing set images are re-keyed from SetNumber to the
/// owning legoset's id (standard GUID string). MOC images keyed under the empty string
/// are ambiguous (single shared doc) and are left untouched.
/// </summary>
public class Migration_002_SetImagesBySetId : IMigration
{
    public int VersionFrom => 1;
    public int VersionTo => 2;
    public string Description =>
        "Re-key set images from the set number to the owning set id (unique per set/MOC).";

    public async Task RunAsync(IMongoDatabase database)
    {
        var legosets = database.GetCollection<BsonDocument>("legosets");
        var images = database.GetCollection<BsonDocument>("set_images");

        var sets = await legosets.Find(_ => true).ToListAsync();

        // Map SetNumber -> set Id (standard GUID string) for sets that have one.
        var byNumber = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            if (!set.TryGetValue("SetNumber", out var numVal)) continue;
            var num = numVal.IsString && !string.IsNullOrEmpty(numVal.AsString) ? numVal.AsString : null;
            if (num is null) continue;
            if (set.TryGetValue("_id", out var idVal) && idVal.IsGuid)
                byNumber[num] = idVal.AsGuid.ToString();
        }

        foreach (var (setNumber, setId) in byNumber)
        {
            // Already re-keyed or a newer image for this id exists: skip.
            var existing = await images
                .Find(Builders<BsonDocument>.Filter.Eq("_id", setId))
                .FirstOrDefaultAsync();
            if (existing is not null) continue;

            var doc = await images
                .Find(Builders<BsonDocument>.Filter.Eq("_id", setNumber))
                .FirstOrDefaultAsync();
            if (doc is null) continue;

            doc["_id"] = setId;
            await images.InsertOneAsync(doc);
            await images.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", setNumber));
        }
    }
}
