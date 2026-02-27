using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BulkPieceDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string LegoId { get; set; } = string.Empty;
    public string LegoColor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public List<StorageAllocationDocument> StorageAllocations { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
