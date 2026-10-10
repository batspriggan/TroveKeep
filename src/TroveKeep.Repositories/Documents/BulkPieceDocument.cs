using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BulkPieceDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Isolation boundary. Nullable only for rows written before migration 006.</summary>
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid CollectionId { get; set; }

    required public string LegoId { get; set; }
    public int LegoColorId { get; set; }
    required public string Description { get; set; }
    required public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool ImageCached { get; set; }
    public int Version { get; set; }
}
