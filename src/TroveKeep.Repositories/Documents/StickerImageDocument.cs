using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

/// <summary>
/// MongoDB row of the global sticker image library. No <c>CollectionId</c>: the library is
/// instance-wide (see <see cref="TroveKeep.Core.Models.StickerImage"/>). The artwork itself lives in
/// <c>set_images</c> with <c>ImageReferenceType.StickerImage</c>, keyed by this id.
/// </summary>
[BsonIgnoreExtraElements]
public class StickerImageDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool ImageCached { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
