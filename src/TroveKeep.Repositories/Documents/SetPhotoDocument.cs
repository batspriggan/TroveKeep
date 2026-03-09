using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

public class SetPhotoDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid SetId { get; set; }

    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
