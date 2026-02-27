using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class SetImageDocument
{
    [BsonId]
    public string SetNum { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "";
    public DateTime DownloadedAt { get; set; }
}
