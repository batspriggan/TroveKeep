using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class LegoSetDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string SetNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsMoc { get; set; }
    public bool ImageCached { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
