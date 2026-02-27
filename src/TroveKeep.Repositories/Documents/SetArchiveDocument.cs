using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class SetArchiveDocument
{
    [BsonId]
    public string SetNum { get; set; } = "";
    public string Name { get; set; } = "";
    public int Year { get; set; }
    public int ThemeId { get; set; }
    public int NumParts { get; set; }
    public string ImgUrl { get; set; } = "";
}
