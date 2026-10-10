using System.Security.Cryptography;
using MongoDB.Bson;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Repositories;

/// <summary>
/// Reads and caches <c>meta/_id="app_config"</c> (see <see cref="IAppStateService"/>). Registered
/// as a singleton: the document changes at most once per process (the first-run setup), and the
/// cache is explicitly invalidated at that point.
/// </summary>
public class AppStateService : IAppStateService
{
    /// <summary>Key of the application-state document inside the shared <c>meta</c> collection.</summary>
    public const string AppConfigKey = "app_config";

    private readonly IMongoCollection<BsonDocument> _meta;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AppState? _cached;

    public AppStateService(IMongoDatabase database)
    {
        _meta = database.GetCollection<BsonDocument>("meta");
    }

    public string? SetupToken { get; private set; }

    public bool RequiresSetupToken => SetupToken is not null;

    public async Task<AppState> GetAsync(CancellationToken ct = default)
    {
        if (_cached is { } cached) return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is { } raced) return raced;

            var doc = await _meta
                .Find(Builders<BsonDocument>.Filter.Eq("_id", AppConfigKey))
                .FirstOrDefaultAsync(ct);

            // A missing document means "never configured": the app stays locked until setup runs.
            _cached = doc is null
                ? new AppState(false, false)
                : new AppState(ReadBool(doc, "initialized"), ReadBool(doc, "multiUserEnabled"));

            return _cached.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(bool initialized, bool multiUserEnabled, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var doc = new BsonDocument
            {
                ["_id"] = AppConfigKey,
                ["initialized"] = initialized,
                ["multiUserEnabled"] = multiUserEnabled,
            };

            await _meta.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", AppConfigKey),
                doc,
                new ReplaceOptions { IsUpsert = true },
                ct);

            _cached = new AppState(initialized, multiUserEnabled);

            // The instance is configured (or deliberately left single-user): the token has served
            // its purpose and must not stay usable. Setup is a one-shot operation.
            SetupToken = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _cached = null;

    public string EnsureSetupToken()
    {
        // Cheap double-check is enough: worst case two threads generate a token and one wins.
        if (SetupToken is { } existing) return existing;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        SetupToken = token;
        return token;
    }

    private static bool ReadBool(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out var value) && value.IsBoolean && value.AsBoolean;
}
