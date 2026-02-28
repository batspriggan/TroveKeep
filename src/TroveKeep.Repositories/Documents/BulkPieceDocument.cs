using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BulkPieceDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    required public string LegoId { get; set; }
    public int LegoColorId { get; set; }
    required public string Description { get; set; }
    required public int Quantity { get; set; }
    required public List<StorageAllocationDocument> StorageAllocations { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool ImageCached { get; set; }
}
