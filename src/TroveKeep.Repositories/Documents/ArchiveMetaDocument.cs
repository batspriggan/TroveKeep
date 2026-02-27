using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class ArchiveMetaDocument
{
    [BsonId]
    public string Key { get; set; } = "";
    public DateTime LastImportedAt { get; set; }
}
