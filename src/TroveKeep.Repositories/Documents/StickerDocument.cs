using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

/// <summary>
/// MongoDB row of the global sticker library. No <c>CollectionId</c>: the library is
/// instance-wide (see <see cref="TroveKeep.Core.Models.Sticker"/>).
/// </summary>
[BsonIgnoreExtraElements]
public class StickerDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public bool ImageCached { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
