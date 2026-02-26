using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IDrawerRepository
{
    Task<Drawer?> GetByIdAsync(Guid id);
    Task<Drawer?> GetByIdWithContentsAsync(Guid id);
    Task<Drawer> CreateAsync(Drawer drawer);
    Task<Drawer?> UpdateAsync(Drawer drawer);
    Task<bool> DeleteAsync(Guid id);
}
