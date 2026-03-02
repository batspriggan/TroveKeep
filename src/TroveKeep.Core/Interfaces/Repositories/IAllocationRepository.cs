using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IAllocationRepository
{
    Task<IEnumerable<StorageAllocation>> GetByItemAsync(Guid itemId);
    Task<IEnumerable<StorageAllocation>> GetByItemsAsync(IEnumerable<Guid> itemIds);
    Task<IEnumerable<StorageAllocation>> GetByStorageAsync(Guid storageId);
    Task<IEnumerable<StorageAllocation>> GetByStoragesAsync(IEnumerable<Guid> storageIds);
    Task<StorageAllocation> AddOrIncrementAsync(Guid itemId, string itemType, Guid storageId, StorageType storageType, int quantity);
    Task<bool> RemoveByItemAndStorageAsync(Guid itemId, Guid storageId);
    Task RemoveAllByItemAsync(Guid itemId);
    Task RemoveAllByStorageAsync(Guid storageId);
}
