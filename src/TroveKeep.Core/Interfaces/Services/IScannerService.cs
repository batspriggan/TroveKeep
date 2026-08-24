using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IScannerService
{
    /// <summary>
    /// Resolves a parsed label reference to the piece/set/box it points to (with storage
    /// allocations where applicable), or null when the entity does not exist.
    /// </summary>
    Task<ScannerResult?> ResolveAsync(LabelRef reference);
}
