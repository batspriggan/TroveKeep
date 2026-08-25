using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ILabelTargetRepository
{
    Task<LabelTarget?> GetByKeyAsync(string key);
    Task<IEnumerable<LabelTarget>> GetByStorageAsync(Guid storageId, int? position = null);
    Task<LabelTarget> UpsertAsync(LabelTarget target);
    Task DeleteByStorageAsync(Guid storageId, int? position = null);
    /// <summary>Moves label targets so they point at a new storage location (used on drawer move).</summary>
    Task UpdateStorageAsync(Guid srcStorageId, int? srcPosition, Guid dstStorageId, int? dstPosition);
}
