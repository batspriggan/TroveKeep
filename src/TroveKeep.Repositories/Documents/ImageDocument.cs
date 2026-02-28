using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class ImageDocument
{
    [BsonId]
    required public string ReferenceNumber { get; set; }
    required public byte[] Data { get; set; }
    required public string ContentType { get; set; }
    required public DateTime DownloadedAt { get; set; }
    required public string ReferenceType {get;set;}
}
