using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IDrawerContainerRepository
{
    Task<IEnumerable<DrawerContainer>> GetAllAsync();
    Task<DrawerContainer?> GetByIdAsync(Guid id);
    Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id);
    Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer);
    Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer);
    Task<bool> DeleteAsync(Guid id);
}
