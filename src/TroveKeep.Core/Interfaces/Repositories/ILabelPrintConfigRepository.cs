using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

/// <summary>Persists keyed application settings (the <c>settings</c> collection).</summary>
public interface ILabelPrintConfigRepository
{
    /// <summary>Returns the persisted label config, or null when none has been saved.</summary>
    Task<LabelPrintConfig?> GetAsync();

    /// <summary>Upserts the label config.</summary>
    Task SaveAsync(LabelPrintConfig config);
}
