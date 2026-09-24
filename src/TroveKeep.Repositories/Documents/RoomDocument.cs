using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class RoomDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public int WidthCm { get; set; }
    public int DepthCm { get; set; }
    public List<PlacedTableDocument> Layout { get; set; } = [];
    public List<AggregateSelectionDocument> AggregateSelections { get; set; } = [];
    public List<AggregateBpLayoutDocument> AggregateBpLayouts { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}

[BsonIgnoreExtraElements]
public class AggregateSelectionDocument
{
    public string RepresentativeId { get; set; } = string.Empty;
    public string BpKey { get; set; } = string.Empty;
}

// Nested documents need their own [BsonIgnoreExtraElements]: the attribute on the parent does
// not apply to embedded types, so a field removed from a nested class would otherwise break
// deserialization of existing documents (e.g. the retired `ModuleGrid`).
[BsonIgnoreExtraElements]
public class AggregateBpLayoutDocument
{
    public string RepresentativeId { get; set; } = string.Empty;
    public List<PlacedBaseplateDocument> PlacedBaseplates { get; set; } = [];
    public int LayoutVersion { get; set; }
}

[BsonIgnoreExtraElements]
public class PlacedBaseplateDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid InstanceId { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BaseplateId { get; set; }
    public int XMm { get; set; }
    public int YMm { get; set; }
    public int Rotation { get; set; }

    /// <summary>MOC/set this placement originates from; null for individually placed plates.</summary>
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? SourceSetId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? PlacementId { get; set; }
}
