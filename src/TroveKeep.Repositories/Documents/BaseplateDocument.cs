using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories.Documents;

[BsonIgnoreExtraElements]
public class BaseplateDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public BaseplateType Type { get; set; } = BaseplateType.Standard;
    public string PartNum { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WidthStuds { get; set; }
    public int DepthStuds { get; set; }
    public int LegoColorId { get; set; }
    public bool ImageCached { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? LinkedSetId { get; set; }

    public RoadShape? RoadShape { get; set; }
    public int Quantity { get; set; } = 1;
    public List<BaseplateReservationDocument> Reservations { get; set; } = [];
    public string? Notes { get; set; }
    public bool NeedsReview { get; set; }
    public bool Quarantined { get; set; }
    public string? QuarantineReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}

[BsonIgnoreExtraElements]
public class BaseplateReservationDocument
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid SetId { get; set; }

    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
