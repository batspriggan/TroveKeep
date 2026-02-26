using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class LegoSetService : ILegoSetService
{
    public Task<IEnumerable<LegoSet>> GetAllAsync() => throw new NotImplementedException();
    public Task<LegoSet?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<LegoSet> CreateAsync(LegoSet legoSet) => throw new NotImplementedException();
    public Task<LegoSet?> UpdateAsync(LegoSet legoSet) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<StorageLocation?> GetStorageAsync(Guid id) => throw new NotImplementedException();
    public Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId) => throw new NotImplementedException();
    public Task<bool> RemoveStorageAsync(Guid id) => throw new NotImplementedException();
}
