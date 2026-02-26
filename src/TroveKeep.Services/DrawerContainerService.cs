using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerContainerService : IDrawerContainerService
{
    public Task<IEnumerable<DrawerContainer>> GetAllAsync() => throw new NotImplementedException();
    public Task<DrawerContainer?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id) => throw new NotImplementedException();
    public Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer) => throw new NotImplementedException();
    public Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<Drawer?> AddDrawerAsync(Guid containerId, Drawer drawer) => throw new NotImplementedException();
}
