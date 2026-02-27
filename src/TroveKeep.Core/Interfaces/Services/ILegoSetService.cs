using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ILegoSetService
{
    Task<IEnumerable<LegoSet>> GetAllAsync();
    Task<LegoSet?> GetByIdAsync(Guid id);
    Task<LegoSet> CreateAsync(LegoSet legoSet);
    Task<LegoSet?> UpdateAsync(LegoSet legoSet);
    Task<bool> DeleteAsync(Guid id);
    Task<LegoSet?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity);
    Task<LegoSet?> DeallocateStorageAsync(Guid id, Guid storageId);
    Task<LegoSet?> ClearStorageAsync(Guid id);
}
