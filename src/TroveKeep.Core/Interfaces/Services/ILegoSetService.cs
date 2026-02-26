using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ILegoSetService
{
    Task<IEnumerable<LegoSet>> GetAllAsync();
    Task<LegoSet?> GetByIdAsync(Guid id);
    Task<LegoSet> CreateAsync(LegoSet legoSet);
    Task<LegoSet?> UpdateAsync(LegoSet legoSet);
    Task<bool> DeleteAsync(Guid id);
    Task<StorageLocation?> GetStorageAsync(Guid id);
    Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId);
    Task<bool> RemoveStorageAsync(Guid id);
}
