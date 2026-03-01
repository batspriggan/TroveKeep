using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class BaseplateDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string PartNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
