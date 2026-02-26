using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories;

public class DrawerContainerRepository : IDrawerContainerRepository
{
    public Task<IEnumerable<DrawerContainer>> GetAllAsync() => throw new NotImplementedException();
    public Task<DrawerContainer?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id) => throw new NotImplementedException();
    public Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer) => throw new NotImplementedException();
    public Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
}
