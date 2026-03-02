using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

public class DrawerContainerDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<DrawerDocument> Drawers { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
