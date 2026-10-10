using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Introduces the multi-user model: a default collection ("Casa") is created and every existing
/// domain row is stamped with its <c>CollectionId</c>, so no data ends up outside the isolation
/// boundary.
///
/// <para>
/// The migration deliberately does <b>not</b> create the admin user: password hashing belongs to
/// the application layer, so that stays in <c>BootstrapService</c>. The two are linked through a
/// flag in <c>meta</c> (<c>bootstrap_pending</c>) that documents "a default collection exists and
/// nobody owns it yet" for operators reading the database.
/// </para>
///
/// <para>Idempotent: every step only touches documents where the target field is absent.</para>
/// </summary>
public class Migration_006_CollectionsAndUsers : IMigration
{
    public int VersionFrom => 5;
    public int VersionTo => 6;

    public string Description =>
        "Create the default collection, stamp CollectionId on all domain rows, and flag the " +
        "pending admin bootstrap.";

    /// <summary>Collections that hold owned data and therefore need a CollectionId.</summary>
    private static readonly string[] DomainCollections =
    [
        "legosets", "bulkpieces", "boxes", "drawercontainers", "baseplates", "rooms", "table_templates",
    ];

    private const string BootstrapPendingKey = "bootstrap_pending";

    public async Task RunAsync(IMongoDatabase database)
    {
        var collections = database.GetCollection<BsonDocument>("collections");
        var meta = database.GetCollection<BsonDocument>("meta");

        // 1. Reuse an existing default collection if this runs twice, otherwise create one.
        var collectionId = await EnsureDefaultCollectionAsync(database, collections);

        // 2. Stamp the collection id on every existing domain row (idempotent).
        foreach (var name in DomainCollections)
        {
            var target = database.GetCollection<BsonDocument>(name);
            var filter = Builders<BsonDocument>.Filter.Exists("CollectionId", false);
            var update = Builders<BsonDocument>.Update.Set(
                "CollectionId", new BsonBinaryData(collectionId, GuidRepresentation.Standard));
            var result = await target.UpdateManyAsync(filter, update);
            Console.WriteLine($"Migration 006: {result.ModifiedCount} row(s) in '{name}' assigned to the default collection.");
        }

        // 3. Flag that the admin account still has to be created by the application bootstrap.
        await meta.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", BootstrapPendingKey),
            new BsonDocument
            {
                ["_id"] = BootstrapPendingKey,
                ["collectionId"] = new BsonBinaryData(collectionId, GuidRepresentation.Standard),
            },
            new ReplaceOptions { IsUpsert = true });
    }

    private static async Task<Guid> EnsureDefaultCollectionAsync(
        IMongoDatabase database, IMongoCollection<BsonDocument> collections)
    {
        // Prefer a collection already flagged as the migration default, so a re-run does not
        // create a second "Casa".
        var existing = await collections
            .Find(Builders<BsonDocument>.Filter.Eq("IsDefault", true))
            .FirstOrDefaultAsync();
        if (existing is not null && TryReadGuid(existing["_id"], out var existingId))
            return existingId;

        var any = await collections.Find(Builders<BsonDocument>.Filter.Empty).FirstOrDefaultAsync();
        if (any is not null && TryReadGuid(any["_id"], out var anyId))
            return anyId;

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await collections.InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(newId, GuidRepresentation.Standard),
            ["Name"] = "Casa",
            ["Description"] = "Default collection created by migration 006.",
            ["IsDefault"] = true,
            ["CreatedAt"] = now,
            ["UpdatedAt"] = now,
        });
        Console.WriteLine($"Migration 006: created default collection 'Casa' ({newId}).");
        return newId;
    }

    /// <summary>
    /// Reads a Guid stored as Standard binary (subtype 4) or as a string, mirroring the tolerance
    /// used by migration 005 for legacy representations.
    /// </summary>
    private static bool TryReadGuid(BsonValue value, out Guid guid)
    {
        guid = Guid.Empty;
        if (value is null || value.IsBsonNull) return false;

        if (value.BsonType == BsonType.Binary)
        {
            var binary = value.AsBsonBinaryData;
            if (binary.SubType == BsonBinarySubType.UuidStandard && binary.Bytes.Length == 16)
            {
                guid = binary.ToGuid(GuidRepresentation.Standard);
                return true;
            }
            return false;
        }

        return value.BsonType == BsonType.String && Guid.TryParse(value.AsString, out guid);
    }
}
