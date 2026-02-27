using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ILegoSetRepository
{
    Task<IEnumerable<LegoSet>> GetAllAsync();
    Task<LegoSet?> GetByIdAsync(Guid id);
    Task<LegoSet> CreateAsync(LegoSet legoSet);
    Task<LegoSet?> UpdateAsync(LegoSet legoSet);
    Task<bool> DeleteAsync(Guid id);
    Task<LegoSet?> AddStorageAsync(Guid id, Guid storageId, StorageType type, int quantity);
    Task<LegoSet?> RemoveStorageAsync(Guid id, Guid storageId);
    Task<LegoSet?> ClearStorageAsync(Guid id);
    Task<IEnumerable<LegoSet>> SearchAsync(string query);
    Task UpdateImageCachedAsync(Guid id);
}
