using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class ColorDocument
{
    [BsonId]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Rgb { get; set; } = "";
    public bool IsTrans { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
}
