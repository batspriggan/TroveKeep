using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ISetPhotoRepository
{
    /// <summary>Returns photo metadata (no Data bytes) for all photos belonging to a set.</summary>
    Task<IEnumerable<SetPhoto>> GetMetadataBySetIdAsync(Guid setId);

    /// <summary>Returns full photo including Data bytes, or null if not found.</summary>
    Task<SetPhoto?> GetByIdAsync(Guid id);

    /// <summary>Returns photo count per set ID, for the given set IDs.</summary>
    Task<Dictionary<Guid, int>> GetCountsBySetIdsAsync(IEnumerable<Guid> setIds);

    Task<Guid> StoreAsync(Guid setId, byte[] data, string contentType);
    Task<bool> DeleteAsync(Guid id);
    Task DeleteBySetIdAsync(Guid setId);
}
