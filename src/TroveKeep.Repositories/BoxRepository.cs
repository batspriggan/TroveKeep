using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories;

public class BoxRepository : IBoxRepository
{
    public Task<IEnumerable<Box>> GetAllAsync() => throw new NotImplementedException();
    public Task<Box?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<Box?> GetByIdWithContentsAsync(Guid id) => throw new NotImplementedException();
    public Task<Box> CreateAsync(Box box) => throw new NotImplementedException();
    public Task<Box?> UpdateAsync(Box box) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
}
