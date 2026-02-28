using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class PartArchiveDocument
{
    [BsonId]
    public string PartNum { get; set; } = "";
    public string Name { get; set; } = "";
}
