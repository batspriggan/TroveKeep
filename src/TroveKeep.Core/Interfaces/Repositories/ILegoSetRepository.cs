using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ILegoSetRepository
{
    Task<IEnumerable<LegoSet>> GetAllAsync();
    Task<LegoSet?> GetByIdAsync(Guid id);
    Task<LegoSet> CreateAsync(LegoSet legoSet);
    Task<LegoSet?> UpdateAsync(LegoSet legoSet);
    Task<bool> DeleteAsync(Guid id);
    Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId);
    Task<LegoSet?> RemoveStorageAsync(Guid id);
}
