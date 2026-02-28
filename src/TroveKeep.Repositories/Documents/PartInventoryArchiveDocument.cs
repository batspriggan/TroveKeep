using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class PartInventoryArchiveDocument
{
    [BsonId]
    required public string PartNum { get; set; }
    required public string ImageUrl { get; set; }
}
