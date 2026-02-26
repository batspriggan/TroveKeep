using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBoxRepository
{
    Task<IEnumerable<Box>> GetAllAsync();
    Task<Box?> GetByIdAsync(Guid id);
    Task<Box?> GetByIdWithContentsAsync(Guid id);
    Task<Box> CreateAsync(Box box);
    Task<Box?> UpdateAsync(Box box);
    Task<bool> DeleteAsync(Guid id);
}
