using MongoDB.Bson.Serialization.Attributes;

namespace TroveKeep.Repositories.Documents;

/// <summary>
/// A generic keyed setting document in the <c>settings</c> collection
/// (e.g. key <c>label_print</c>).
/// </summary>
public class SettingDocument
{
    [BsonId]
    required public string Key { get; set; }

    public LabelPrintConfigDocument? LabelPrint { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class LabelPrintConfigDocument
{
    required public string Mode { get; set; }
    public string? ServerUrl { get; set; }
    public string? ServerToken { get; set; }
}
