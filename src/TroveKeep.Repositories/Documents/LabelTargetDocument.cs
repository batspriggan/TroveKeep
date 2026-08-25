using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class LabelTargetDocument
{
    [BsonId]
    required public string Key { get; set; }
    required public string TargetType { get; set; }
    required public Guid StorageId { get; set; }
    public int? StoragePosition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
