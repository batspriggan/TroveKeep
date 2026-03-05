using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BoxDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool ImageCached { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
