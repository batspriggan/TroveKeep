using MongoDB.Bson;
using MongoDB.Driver;

namespace TroveKeep.Migrations;

/// <summary>
/// Introduces the multi-user model: a default collection is created, every existing domain row is
/// stamped with its <c>CollectionId</c>, and a non-loggable technical account is created to own
/// that collection until the operator completes the first-run setup.
///
/// <para>
/// No password is hashed here on purpose: the technical account has <c>PasswordHash = null</c> and
/// <c>Active = false</c>, so it can never be used to log in. It only provides the admin identity
/// that single-user mode operates as, and it is overwritten by <c>POST /api/setup</c> when
/// multi-user is enabled.
/// </para>
///
/// <para>
/// The application mode is written to <c>meta/_id="app_config"</c> with <c>initialized=false</c>:
/// the API stays locked (503) until the operator runs the setup.
/// </para>
///
/// <para>Idempotent: every step only touches documents that are missing or not yet migrated.</para>
/// </summary>
public class Migration_006_CollectionsAndUsers : IMigration
{
    public int VersionFrom => 5;
    public int VersionTo => 6;

    public string Description =>
        "Create the default collection, stamp CollectionId on all domain rows, seed the technical " +
        "account, and write the uninitialized app_config.";

    /// <summary>Collections that hold owned data and therefore need a CollectionId.</summary>
    private static readonly string[] DomainCollections =
    [
        "legosets", "bulkpieces", "boxes", "drawercontainers", "baseplates", "rooms", "table_templates",
    ];

    /// <summary>Email of the non-loggable technical account (matches the documented default).</summary>
    public const string SystemUserEmail = "system@trovekeep.local";

    private readonly string _defaultCollectionName;

    public Migration_006_CollectionsAndUsers(string defaultCollectionName = "Casa")
    {
        _defaultCollectionName = string.IsNullOrWhiteSpace(defaultCollectionName) ? "Casa" : defaultCollectionName;
    }

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

        // 3. Seed the technical account and its ownership of the default collection. Skipped as
        //    soon as any user exists (a database that already went through setup keeps its users).
        var userId = await EnsureSystemUserAsync(database);
        if (userId is { } systemUserId)
            await EnsureOwnerMembershipAsync(database, collectionId, systemUserId);

        // 4. Application mode: lock the API until the first-run setup completes.
        await meta.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "app_config"),
            Builders<BsonDocument>.Update
                .SetOnInsert("initialized", false)
                .SetOnInsert("multiUserEnabled", false),
            new UpdateOptions { IsUpsert = true });

        // 5. Drop the obsolete pre-setup flag of the previous iteration of this migration.
        var removed = await meta.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "bootstrap_pending"));
        if (removed.DeletedCount > 0)
            Console.WriteLine("Migration 006: removed obsolete meta.bootstrap_pending flag.");
    }

    private async Task<Guid> EnsureDefaultCollectionAsync(
        IMongoDatabase database, IMongoCollection<BsonDocument> collections)
    {
        // Prefer a collection already flagged as the migration default, so a re-run does not
        // create a second default collection.
        var existing = await collections
            .Find(Builders<BsonDocument>.Filter.Eq("IsDefault", true))
            .FirstOrDefaultAsync();
        if (existing is not null && TryReadGuid(existing["_id"], out var existingId))
            return existingId;

        // Any other collection is reused as well: never orphan data behind a second tenant.
        var any = await collections.Find(Builders<BsonDocument>.Filter.Empty).FirstOrDefaultAsync();
        if (any is not null && TryReadGuid(any["_id"], out var anyId))
            return anyId;

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await collections.InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(newId, GuidRepresentation.Standard),
            ["Name"] = _defaultCollectionName,
            ["Description"] = "Collezione predefinita creata automaticamente. Raccoglie tutti i dati esistenti prima dell'introduzione degli utenti.",
            ["IsDefault"] = true,
            ["CreatedAt"] = now,
            ["UpdatedAt"] = now,
        });
        Console.WriteLine($"Migration 006: created default collection '{_defaultCollectionName}' ({newId}).");
        return newId;
    }

    /// <summary>
    /// Creates the technical account when the database has no user at all. Returns its id, or
    /// <c>null</c> when a user already exists (nothing to do).
    /// </summary>
    private static async Task<Guid?> EnsureSystemUserAsync(IMongoDatabase database)
    {
        var users = database.GetCollection<BsonDocument>("users");
        var any = await users.Find(Builders<BsonDocument>.Filter.Empty).FirstOrDefaultAsync();
        if (any is not null)
        {
            Console.WriteLine("Migration 006: users already exist; technical account not needed.");
            return null;
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await users.InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
            ["Email"] = SystemUserEmail,
            ["DisplayName"] = "System",
            ["PasswordHash"] = BsonNull.Value,
            ["Role"] = 2, // UserRole.Admin
            ["Active"] = false,
            ["IsSystem"] = true,
            ["ExternalProvider"] = BsonNull.Value,
            ["ExternalSubject"] = BsonNull.Value,
            ["CreatedAt"] = now,
            ["UpdatedAt"] = now,
        });
        Console.WriteLine($"Migration 006: created technical account '{SystemUserEmail}' ({id}).");
        return id;
    }

    private static async Task EnsureOwnerMembershipAsync(IMongoDatabase database, Guid collectionId, Guid userId)
    {
        var members = database.GetCollection<BsonDocument>("collection_members");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("CollectionId", new BsonBinaryData(collectionId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Filter.Eq("UserId", new BsonBinaryData(userId, GuidRepresentation.Standard)));

        if (await members.Find(filter).AnyAsync()) return;

        await members.InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["CollectionId"] = new BsonBinaryData(collectionId, GuidRepresentation.Standard),
            ["UserId"] = new BsonBinaryData(userId, GuidRepresentation.Standard),
            ["Access"] = 2, // CollectionAccess.Owner
            ["AddedAt"] = DateTime.UtcNow,
        });
        Console.WriteLine("Migration 006: technical account linked as Owner of the default collection.");
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
