using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories;

public class LegoSetRepository : ILegoSetRepository
{
    public Task<IEnumerable<LegoSet>> GetAllAsync() => throw new NotImplementedException();
    public Task<LegoSet?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<LegoSet> CreateAsync(LegoSet legoSet) => throw new NotImplementedException();
    public Task<LegoSet?> UpdateAsync(LegoSet legoSet) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId) => throw new NotImplementedException();
    public Task<LegoSet?> RemoveStorageAsync(Guid id) => throw new NotImplementedException();
}
