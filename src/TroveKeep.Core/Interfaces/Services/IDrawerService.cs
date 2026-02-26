using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IDrawerService
{
    Task<Drawer?> GetByIdAsync(Guid id);
    Task<Drawer?> GetByIdWithContentsAsync(Guid id);
    Task<Drawer?> UpdateAsync(Drawer drawer);
    Task<bool> DeleteAsync(Guid id);
}
