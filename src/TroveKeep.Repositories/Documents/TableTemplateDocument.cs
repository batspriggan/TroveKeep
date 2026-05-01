using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class TableTemplateDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Description { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public string Color { get; set; } = "#8b6340";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
