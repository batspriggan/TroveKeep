using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IAllocationRepository
{
    Task<IEnumerable<StorageAllocation>> GetByItemAsync(Guid itemId);
    Task<IEnumerable<StorageAllocation>> GetByItemsAsync(IEnumerable<Guid> itemIds);
    Task<IEnumerable<StorageAllocation>> GetByStorageAsync(Guid storageId, int? position = null);
    Task<IEnumerable<StorageAllocation>> GetByStoragesAsync(IEnumerable<Guid> storageIds);
    Task<StorageAllocation> AddOrIncrementAsync(Guid itemId, string itemType, Guid storageId, StorageType storageType, int quantity, int? storagePosition = null);
    Task<bool> RemoveByItemAndStorageAsync(Guid itemId, Guid storageId, int? storagePosition = null);
    Task RemoveAllByItemAsync(Guid itemId);
    Task RemoveAllByStorageAsync(Guid storageId, int? position = null);
}
