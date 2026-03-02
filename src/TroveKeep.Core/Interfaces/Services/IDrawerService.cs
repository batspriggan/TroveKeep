using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IDrawerService
{
    Task<Drawer?> GetByPositionAsync(Guid containerId, int position);
    Task<Drawer?> GetByPositionWithContentsAsync(Guid containerId, int position);
    Task<Drawer?> UpdateAsync(Drawer drawer);
    Task<bool> DeleteAsync(Guid containerId, int position);
}
