using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Flags all bulk pieces as having no cached image (issue #2).
///
/// Part images are now addressed per (partNum, colorId). Existing piece images were saved
/// without a color, so they are no longer the correct image for the piece's color. Rather than
/// deleting them, this migration only sets <c>ImageCached = false</c> on every bulk piece: the
/// old images are left in place (unreferenced) and the correct per-color image is fetched on
/// demand when the piece is opened (lazy re-index in <c>GET /api/bulkpieces/{id}/image</c>),
/// which then re-sets ImageCached to true.
/// </summary>
public class Migration_003_PartImagesByColor : IMigration
{
    public int VersionFrom => 2;
    public int VersionTo => 3;
    public string Description =>
        "Mark all bulk pieces ImageCached=false so their per-color image is re-fetched lazily.";

    public async Task RunAsync(IMongoDatabase database)
    {
        var bulkPieces = database.GetCollection<BsonDocument>("bulkpieces");
        await bulkPieces.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Empty,
            Builders<BsonDocument>.Update.Set("ImageCached", false));
    }
}
