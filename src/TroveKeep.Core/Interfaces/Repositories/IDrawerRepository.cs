using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IDrawerRepository
{
    Task<Drawer?> GetByPositionAsync(Guid containerId, int position);
    Task<Drawer?> GetByPositionWithContentsAsync(Guid containerId, int position);
    Task<Drawer> CreateAsync(Drawer drawer);
    Task<Drawer?> UpdateAsync(Drawer drawer);
    Task<bool> DeleteAsync(Guid containerId, int position);
}
