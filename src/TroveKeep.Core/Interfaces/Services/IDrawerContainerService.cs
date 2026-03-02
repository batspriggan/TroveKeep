using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IDrawerContainerService
{
    Task<IEnumerable<DrawerContainer>> GetAllAsync();
    Task<DrawerContainer?> GetByIdAsync(Guid id);
    Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id);
    Task<DrawerContainer> CreateAsync(string name, string? description, int drawerCount);
    Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer);
    Task<bool> DeleteAsync(Guid id);
    Task<Drawer?> AddDrawerAsync(Guid containerId, Drawer drawer);
}
