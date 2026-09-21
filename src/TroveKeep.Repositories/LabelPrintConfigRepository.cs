using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class LabelPrintConfigRepository : ILabelPrintConfigRepository
{
    private const string Key = "label_print";

    private readonly IMongoCollection<SettingDocument> _collection;

    public LabelPrintConfigRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SettingDocument>("settings");
    }

    public async Task<LabelPrintConfig?> GetAsync()
    {
        var doc = await _collection.Find(x => x.Key == Key).FirstOrDefaultAsync();
        if (doc?.LabelPrint is null) return null;

        return new LabelPrintConfig
        {
            Mode = doc.LabelPrint.Mode,
            ServerUrl = doc.LabelPrint.ServerUrl,
            ServerToken = doc.LabelPrint.ServerToken,
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        };
    }

    public async Task SaveAsync(LabelPrintConfig config)
    {
        var doc = new SettingDocument
        {
            Key = Key,
            LabelPrint = new LabelPrintConfigDocument
            {
                Mode = config.Mode,
                ServerUrl = config.ServerUrl,
                ServerToken = config.ServerToken,
            },
            UpdatedAt = DateTime.UtcNow,
        };

        await _collection.ReplaceOneAsync(
            Builders<SettingDocument>.Filter.Eq(x => x.Key, Key),
            doc,
            new ReplaceOptions { IsUpsert = true });
    }
}
