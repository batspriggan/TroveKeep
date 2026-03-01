using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class PlacedTableDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid InstanceId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid TemplateId { get; set; }

    public int XCm { get; set; }
    public int YCm { get; set; }
}
