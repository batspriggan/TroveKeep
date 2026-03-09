using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BaseplateDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public BaseplateType Type { get; set; } = BaseplateType.Standard;
    public string PartNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public int LegoColorId { get; set; }
    public bool ImageCached { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? LinkedSetId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
