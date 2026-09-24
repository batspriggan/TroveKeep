using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class DrawerDocument
{
    public int Position { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
