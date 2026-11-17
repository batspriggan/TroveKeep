using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories.Documents;

/// <summary>
/// MongoDB row of a global sticker project. No <c>CollectionId</c>: projects are instance-wide, like
/// the image library they draw from. The items are embedded — a project is one document.
/// </summary>
[BsonIgnoreExtraElements]
public class StickerProjectDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public List<StickerItemDocument> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>Embedded item of a sticker project (no own collection, no own version).</summary>
[BsonIgnoreExtraElements]
public class StickerItemDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid ImageId { get; set; }

    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public int Copies { get; set; } = 1;
    public StickerFit Fit { get; set; } = StickerFit.Contain;
    public string? Notes { get; set; }
}
