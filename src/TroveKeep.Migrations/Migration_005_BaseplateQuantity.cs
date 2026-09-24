using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories;
using TroveKeep.Services;

namespace TroveKeep.Migrations;

/// <summary>
/// Migrates the baseplate catalogue to the quantity/reservation/review model, deduplicates
/// legacy rows that describe the same physical baseplate type, and dissolves legacy MOC rows
/// (<c>Type == Custom &amp;&amp; LinkedSetId != null</c>) into real baseplate placements.
///
/// Steps (all idempotent and additive, the runner takes the pre-migration backup):
/// <list type="number">
///   <item>backfill the new fields where they are absent: <c>Quantity=1</c>,
///         <c>RoadShape=null</c>, <c>Reservations=[]</c>, <c>Notes=null</c>,
///         <c>NeedsReview=true</c> (rows that were never verified), <c>Quarantined=false</c>,
///         <c>QuarantineReason=null</c>;</item>
///   <item>merge rows sharing the business key
///         <c>Type|PartNum|LegoColorId|WidthStuds|DepthStuds</c> into a single canonical
///         row (oldest <c>CreatedAt</c>, <c>_id</c> as deterministic tie-break), summing
///         <c>Quantity</c> and merging <c>Reservations</c> per <c>SetId</c>, then
///         remapping <c>rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId</c>
///         references from the dropped rows to the canonical one before deleting them;</item>
///   <item>dissolve every remaining MOC row through <see cref="IMocReconciliationService"/>:
///         reserve the real module plates for the linked set, rewrite each placement into a
///         cols×rows grid of real plates carrying <c>SourceSetId</c>, and delete the MOC row.
///         A row that cannot be converted is <b>quarantined</b> (kept, flagged) and recorded in
///         <c>meta</c> under <c>migration_005_unresolved</c>; the migration never aborts for a
///         single bad row. A final query checks no placement points at a deleted row.</item>
/// </list>
/// Any legacy <c>ModuleGrid</c> left in older room documents is simply ignored: typed room
/// documents use <c>[BsonIgnoreExtraElements]</c>, and the field is no longer part of the model.
/// </summary>
public class Migration_005_BaseplateQuantity : IMigration
{
    private const string UnresolvedMetaKey = "migration_005_unresolved";

    public int VersionFrom => 4;
    public int VersionTo => 5;

    public string Description =>
        "Backfill baseplate Quantity/RoadShape/Reservations/Notes/NeedsReview/Quarantine, deduplicate " +
        "catalogue rows, and dissolve legacy MOC rows into real placements.";

    public async Task RunAsync(IMongoDatabase database)
    {
        var baseplates = database.GetCollection<BsonDocument>("baseplates");

        // 1. Additive backfill of the new fields where absent.
        await BackfillAsync(baseplates, "Quantity", 1);
        await BackfillAsync(baseplates, "RoadShape", BsonNull.Value);
        await BackfillAsync(baseplates, "Reservations", new BsonArray());
        await BackfillAsync(baseplates, "Notes", BsonNull.Value);
        await BackfillAsync(baseplates, "NeedsReview", true);
        await BackfillAsync(baseplates, "Quarantined", false);
        await BackfillAsync(baseplates, "QuarantineReason", BsonNull.Value);

        // 2. Deduplicate rows that share the business key.
        await DeduplicateAsync(database, baseplates);

        // 3. Dissolve legacy MOC rows (fail-soft, row by row).
        await DissolveMocRowsAsync(database);
    }

    private static Task<UpdateResult> BackfillAsync(
        IMongoCollection<BsonDocument> collection, string field, BsonValue value)
    {
        var filter = Builders<BsonDocument>.Filter.Exists(field, false);
        var update = Builders<BsonDocument>.Update.Set(field, value);
        return collection.UpdateManyAsync(filter, update);
    }

    private static async Task DeduplicateAsync(
        IMongoDatabase database, IMongoCollection<BsonDocument> baseplates)
    {
        var documents = await baseplates.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();

        // Group by business key. Documents that cannot be keyed reliably (missing/invalid
        // business fields or _id) are skipped: they are never merged nor deleted, and a
        // warning is emitted so the loss of automation is visible (fail-soft, never silent).
        var groups = new Dictionary<string, List<(BsonDocument Document, Guid Id)>>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            if (!TryReadGuid(document.GetValue("_id", BsonNull.Value), out var id))
            {
                Console.WriteLine(
                    "[Migration_005] WARNING: skipping baseplate without a valid Guid _id; " +
                    "it will not take part in deduplication.");
                continue;
            }

            if (!TryBuildBusinessKey(document, out var key))
            {
                Console.WriteLine(
                    $"[Migration_005] WARNING: skipping malformed baseplate {id}: business key " +
                    "fields (Type, PartNum, LegoColorId, WidthStuds, DepthStuds) missing or invalid.");
                continue;
            }

            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = [];
                groups[key] = bucket;
            }

            bucket.Add((document, id));
        }

        // duplicate id -> canonical id, applied to the rooms collection before deletion.
        var remap = new Dictionary<Guid, Guid>();

        foreach (var group in groups.Values)
        {
            if (group.Count < 2) continue;

            // Canonical = oldest CreatedAt, with a deterministic tie-break on _id.
            var ordered = group
                .OrderBy(entry => ReadCreatedAt(entry.Document))
                .ThenBy(entry => entry.Id)
                .ToList();

            var canonical = ordered[0];
            var duplicates = ordered.Skip(1).ToList();

            var mergedQuantity = 0;
            var mergedReservations = new List<BsonDocument>();
            var reservationIndex = new Dictionary<Guid, int>();

            foreach (var (document, _) in ordered)
            {
                mergedQuantity += ReadInt(document, "Quantity", 1);

                if (!document.TryGetValue("Reservations", out var reservationsValue) ||
                    reservationsValue.IsBsonNull)
                {
                    continue;
                }

                if (!reservationsValue.IsBsonArray)
                {
                    Console.WriteLine(
                        $"[Migration_005] WARNING: baseplate {document["_id"]} has a non-array " +
                        "Reservations value; its reservations are ignored in the merge.");
                    continue;
                }

                foreach (var item in reservationsValue.AsBsonArray)
                {
                    if (!item.IsBsonDocument)
                    {
                        Console.WriteLine(
                            $"[Migration_005] WARNING: baseplate {document["_id"]} has a malformed " +
                            "reservation entry; the entry is ignored in the merge.");
                        continue;
                    }

                    var reservation = item.AsBsonDocument;
                    if (!TryReadGuid(reservation.GetValue("SetId", BsonNull.Value), out var setId))
                    {
                        Console.WriteLine(
                            $"[Migration_005] WARNING: baseplate {document["_id"]} has a reservation " +
                            "without a valid SetId; the entry is ignored in the merge.");
                        continue;
                    }

                    var quantity = ReadInt(reservation, "Quantity", 0);

                    if (reservationIndex.TryGetValue(setId, out var index))
                    {
                        mergedReservations[index]["Quantity"] =
                            ReadInt(mergedReservations[index], "Quantity", 0) + quantity;
                        continue;
                    }

                    reservationIndex[setId] = mergedReservations.Count;
                    mergedReservations.Add(new BsonDocument
                    {
                        ["SetId"] = new BsonBinaryData(setId, GuidRepresentation.Standard),
                        ["Quantity"] = quantity,
                        // First occurrence wins, so the merge stays deterministic.
                        ["CreatedAt"] = reservation.TryGetValue("CreatedAt", out var createdAt)
                            ? createdAt
                            : new BsonDateTime(DateTime.UnixEpoch),
                    });
                }
            }

            var canonicalId = new BsonBinaryData(canonical.Id, GuidRepresentation.Standard);
            await baseplates.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", canonicalId),
                Builders<BsonDocument>.Update
                    .Set("Quantity", mergedQuantity)
                    .Set("Reservations", new BsonArray(mergedReservations))
                    .Set("NeedsReview", true));

            foreach (var (_, duplicateId) in duplicates)
            {
                remap[duplicateId] = canonical.Id;
            }

            Console.WriteLine(
                $"[Migration_005] merged {group.Count} baseplates into {canonical.Id} " +
                $"({duplicates.Count} duplicate(s), quantity {mergedQuantity}).");
        }

        if (remap.Count == 0) return;

        // Remap layout references before deleting the duplicates so no room ever points
        // at a row that no longer exists.
        await RemapRoomReferencesAsync(database, remap);

        var duplicateIds = remap.Keys
            .Select(id => (BsonValue)new BsonBinaryData(id, GuidRepresentation.Standard))
            .ToList();
        await baseplates.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", duplicateIds));
    }

    /// <summary>
    /// Dissolves every remaining <c>Custom + LinkedSetId</c> row through the shared reconciliation
    /// service. <c>Migration_005</c> has no DI container, so the repository/service graph is built
    /// by hand from the <see cref="IMongoDatabase"/> (the same object graph DI wires for the API):
    /// the service deliberately depends only on repository interfaces, which makes this possible.
    /// </summary>
    private static async Task DissolveMocRowsAsync(IMongoDatabase database)
    {
        var baseplateRepo = new BaseplateRepository(database);
        var roomRepo = new RoomRepository(database);
        var imageRepo = new ImageRepository(database);
        var setRepo = new LegoSetRepository(database);

        IMocReconciliationService reconciliation =
            new MocReconciliationService(baseplateRepo, roomRepo, imageRepo);

        // Identify the candidates with a raw BSON query (Type == Custom, LinkedSetId present) so a
        // single malformed document cannot break the whole scan; each row is then loaded and
        // processed inside its own try/catch.
        var baseplates = database.GetCollection<BsonDocument>("baseplates");
        var mocFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("Type", (int)BaseplateType.Custom),
            Builders<BsonDocument>.Filter.Ne("LinkedSetId", BsonNull.Value));

        var mocIdDocs = await baseplates.Find(mocFilter)
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync();

        var unresolved = new List<(Guid Id, string Reason)>();

        foreach (var idDoc in mocIdDocs)
        {
            if (!TryReadGuid(idDoc.GetValue("_id", BsonNull.Value), out var mocId))
            {
                Console.WriteLine("[Migration_005] WARNING: skipping MOC row without a valid Guid _id.");
                continue;
            }

            try
            {
                var moc = await baseplateRepo.GetByIdAsync(mocId);
                if (moc is null) continue;

                var target = await reconciliation.FindModuleAsync(
                    moc.WidthStuds, moc.DepthStuds, moc.LegoColorId, moc.Id);
                if (target is null)
                {
                    await RecordUnresolvedAsync(reconciliation, unresolved, moc.Id, "no matching module");
                    continue;
                }

                var linkedSet = await setRepo.GetByIdAsync(moc.LinkedSetId!.Value);
                if (linkedSet is null)
                {
                    await RecordUnresolvedAsync(reconciliation, unresolved, moc.Id, "linked set not found");
                    continue;
                }

                if (!TryDeriveArrangement(moc, target, out var cols, out var rows))
                {
                    await RecordUnresolvedAsync(reconciliation, unresolved, moc.Id, "no matching module");
                    continue;
                }

                var result = await reconciliation.DissolveAsync(moc.Id, target.Id, cols, rows);
                if (!result.Dissolved)
                {
                    await RecordUnresolvedAsync(
                        reconciliation, unresolved, moc.Id, result.Reason ?? "dissolution failed");
                    continue;
                }

                Console.WriteLine(
                    $"[Migration_005] dissolved MOC {moc.Id} into {cols}x{rows} x {target.Id} " +
                    $"({result.PlacementsRewritten} placement(s) rewritten).");
            }
            catch (Exception ex)
            {
                // Fail-soft per row: quarantine and keep going, never abort the whole migration.
                await RecordUnresolvedAsync(reconciliation, unresolved, mocId, $"dissolution error: {ex.Message}");
            }
        }

        await UpsertUnresolvedAsync(database, unresolved);
        await LogDanglingReferencesAsync(database);
    }

    /// <summary>
    /// Quarantines a row and records the reason in the in-memory unresolved list. Quarantining
    /// itself is best-effort: a failure to flag the row is logged but must not abort the migration.
    /// </summary>
    private static async Task RecordUnresolvedAsync(
        IMocReconciliationService reconciliation,
        List<(Guid Id, string Reason)> unresolved,
        Guid baseplateId,
        string reason)
    {
        unresolved.Add((baseplateId, reason));

        try
        {
            await reconciliation.QuarantineAsync(baseplateId, reason);
            Console.WriteLine($"[Migration_005] MOC {baseplateId} quarantined: {reason}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Migration_005] WARNING: could not quarantine {baseplateId} ({reason}): {ex.Message}");
        }
    }

    /// <summary>
    /// Derives the cols×rows arrangement of the real module inside the MOC footprint. Returns
    /// false when the module does not divide the footprint in either orientation.
    /// </summary>
    private static bool TryDeriveArrangement(Baseplate moc, Baseplate target, out int cols, out int rows)
    {
        cols = 0;
        rows = 0;

        if (target.WidthStuds <= 0 || target.DepthStuds <= 0) return false;

        if (moc.WidthStuds % target.WidthStuds == 0 && moc.DepthStuds % target.DepthStuds == 0)
        {
            cols = moc.WidthStuds / target.WidthStuds;
            rows = moc.DepthStuds / target.DepthStuds;
            return cols > 0 && rows > 0;
        }

        if (moc.WidthStuds % target.DepthStuds == 0 && moc.DepthStuds % target.WidthStuds == 0)
        {
            // Swapped orientation: the module is rotated inside the MOC footprint.
            cols = moc.WidthStuds / target.DepthStuds;
            rows = moc.DepthStuds / target.WidthStuds;
            return cols > 0 && rows > 0;
        }

        return false;
    }

    /// <summary>
    /// Upserts the list of MOC rows that could not be dissolved into <c>meta</c>, so an operator
    /// can inspect them. Always written (empty array when everything succeeded) to keep the value
    /// meaningful across re-runs.
    /// </summary>
    private static async Task UpsertUnresolvedAsync(
        IMongoDatabase database, IReadOnlyList<(Guid Id, string Reason)> unresolved)
    {
        var meta = database.GetCollection<BsonDocument>("meta");
        var array = new BsonArray(unresolved.Select(entry => new BsonDocument
        {
            ["baseplateId"] = new BsonBinaryData(entry.Id, GuidRepresentation.Standard),
            ["reason"] = entry.Reason,
        }));

        await meta.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", UnresolvedMetaKey),
            new BsonDocument
            {
                ["_id"] = UnresolvedMetaKey,
                ["unresolved"] = array,
            },
            new ReplaceOptions { IsUpsert = true });
    }

    /// <summary>
    /// Final control query: no <c>PlacedBaseplate</c> may reference a baseplate row that no longer
    /// exists. A problem is only logged (never aborts), as required for third-party installs.
    /// </summary>
    private static async Task LogDanglingReferencesAsync(IMongoDatabase database)
    {
        var baseplates = database.GetCollection<BsonDocument>("baseplates");
        var idDocs = await baseplates.Find(Builders<BsonDocument>.Filter.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync();

        var known = new HashSet<Guid>();
        foreach (var doc in idDocs)
        {
            if (TryReadGuid(doc.GetValue("_id", BsonNull.Value), out var id)) known.Add(id);
        }

        var rooms = await database.GetCollection<BsonDocument>("rooms")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync();

        var dangling = 0;
        foreach (var room in rooms)
        {
            if (!room.TryGetValue("AggregateBpLayouts", out var layoutsValue) || !layoutsValue.IsBsonArray)
                continue;

            foreach (var layoutItem in layoutsValue.AsBsonArray)
            {
                if (!layoutItem.IsBsonDocument) continue;

                if (!layoutItem.AsBsonDocument.TryGetValue("PlacedBaseplates", out var placedValue) ||
                    !placedValue.IsBsonArray)
                {
                    continue;
                }

                foreach (var placedItem in placedValue.AsBsonArray)
                {
                    if (!placedItem.IsBsonDocument) continue;

                    if (!TryReadGuid(placedItem.AsBsonDocument.GetValue("BaseplateId", BsonNull.Value), out var bpId))
                        continue;

                    if (!known.Contains(bpId)) dangling++;
                }
            }
        }

        Console.WriteLine(dangling > 0
            ? $"[Migration_005] WARNING: {dangling} PlacedBaseplate(s) still reference a missing baseplate row."
            : "[Migration_005] reference check OK: no dangling PlacedBaseplate references.");
    }

    /// <summary>
    /// Rewrites <c>rooms[].AggregateBpLayouts[].PlacedBaseplates[].BaseplateId</c> so every
    /// reference to a merged-away baseplate points at its canonical row. Rooms whose layout
    /// holds no stale reference are left completely untouched.
    /// </summary>
    private static async Task RemapRoomReferencesAsync(
        IMongoDatabase database, IReadOnlyDictionary<Guid, Guid> remap)
    {
        var rooms = database.GetCollection<BsonDocument>("rooms");
        var roomDocuments = await rooms.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();

        foreach (var room in roomDocuments)
        {
            if (!room.TryGetValue("AggregateBpLayouts", out var layoutsValue) ||
                !layoutsValue.IsBsonArray)
            {
                continue;
            }

            var changed = false;

            foreach (var layoutItem in layoutsValue.AsBsonArray)
            {
                if (!layoutItem.IsBsonDocument) continue;

                var layout = layoutItem.AsBsonDocument;
                if (!layout.TryGetValue("PlacedBaseplates", out var placedValue) ||
                    !placedValue.IsBsonArray)
                {
                    continue;
                }

                foreach (var placedItem in placedValue.AsBsonArray)
                {
                    if (!placedItem.IsBsonDocument) continue;

                    var placed = placedItem.AsBsonDocument;
                    if (!TryReadGuid(placed.GetValue("BaseplateId", BsonNull.Value), out var baseplateId))
                    {
                        continue;
                    }

                    if (remap.TryGetValue(baseplateId, out var canonicalId))
                    {
                        placed["BaseplateId"] = new BsonBinaryData(canonicalId, GuidRepresentation.Standard);
                        changed = true;
                    }
                }
            }

            if (!changed) continue;

            if (!TryReadGuid(room.GetValue("_id", BsonNull.Value), out var roomId))
            {
                Console.WriteLine(
                    "[Migration_005] WARNING: a room document with remapped layout references " +
                    "has no valid Guid _id and could not be persisted; its baseplate references " +
                    "may still point at a removed row.");
                continue;
            }

            await rooms.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq(
                    "_id", new BsonBinaryData(roomId, GuidRepresentation.Standard)),
                room);
        }
    }

    /// <summary>
    /// Business key of a baseplate: <c>Type|PartNum|LegoColorId|WidthStuds|DepthStuds</c>.
    /// Returns false when any component is missing or has an unexpected BSON type.
    /// </summary>
    private static bool TryBuildBusinessKey(BsonDocument document, out string key)
    {
        key = string.Empty;

        if (!TryReadInt(document, "Type", out var type)) return false;
        if (!document.TryGetValue("PartNum", out var partNum) || !partNum.IsString) return false;
        if (!TryReadInt(document, "LegoColorId", out var colorId)) return false;
        if (!TryReadInt(document, "WidthStuds", out var widthStuds)) return false;
        if (!TryReadInt(document, "DepthStuds", out var depthStuds)) return false;

        key = $"{type}|{partNum.AsString}|{colorId}|{widthStuds}|{depthStuds}";
        return true;
    }

    /// <summary>
    /// Converts a BSON value to a Guid using the project's Standard representation.
    /// Only subtype 4 binary (Guid Standard) and plain string Guids are accepted; anything
    /// else is treated as malformed so it can be skipped rather than misinterpreted.
    /// </summary>
    private static bool TryReadGuid(BsonValue? value, out Guid guid)
    {
        guid = Guid.Empty;

        if (value is null || value.IsBsonNull) return false;

        if (value.IsBsonBinaryData)
        {
            var binary = value.AsBsonBinaryData;
            if (binary.SubType != BsonBinarySubType.UuidStandard || binary.Bytes.Length != 16)
                return false;

            guid = binary.ToGuid(GuidRepresentation.Standard);
            return true;
        }

        if (value.IsString && Guid.TryParse(value.AsString, out var parsed))
        {
            guid = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadInt(BsonDocument document, string field, out int value)
    {
        value = 0;
        if (!document.TryGetValue(field, out var raw) || !raw.IsNumeric) return false;

        value = raw.ToInt32();
        return true;
    }

    private static int ReadInt(BsonDocument document, string field, int fallback) =>
        TryReadInt(document, field, out var value) ? value : fallback;

    /// <summary>
    /// Sort key for picking the canonical row: documents without a usable CreatedAt sort
    /// last, so a dated row is always preferred.
    /// </summary>
    private static DateTime ReadCreatedAt(BsonDocument document)
    {
        if (document.TryGetValue("CreatedAt", out var value) && value.IsValidDateTime)
            return value.ToUniversalTime();

        return DateTime.MaxValue;
    }
}
