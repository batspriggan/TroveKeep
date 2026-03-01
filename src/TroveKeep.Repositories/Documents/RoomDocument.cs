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
    public List<AggregateSelectionDocument> AggregateSelections { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AggregateSelectionDocument
{
    public string RepresentativeId { get; set; } = string.Empty;
    public string BpKey { get; set; } = string.Empty;
}
