using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class RoomDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public List<PlacedTableDocument> Layout { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
