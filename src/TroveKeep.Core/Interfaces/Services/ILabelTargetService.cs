using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ILabelTargetService
{
    /// <summary>
    /// Returns the stable neutral QR key associated with a physical storage location
    /// (box or drawer), creating it on first use. The same location keeps the same key
    /// across re-prints, so the printed QR stays valid.
    /// </summary>
    Task<string> GetOrCreateStorageKeyAsync(StorageType type, Guid storageId, int? position = null);
}
