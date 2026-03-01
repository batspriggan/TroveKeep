using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class PartCategoryDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
