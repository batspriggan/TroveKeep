using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class StorageAllocationDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid StorageId { get; set; }
    public string StorageType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
