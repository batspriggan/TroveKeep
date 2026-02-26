using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerContainerService : IDrawerContainerService
{
    private readonly IDrawerContainerRepository _containerRepo;
    private readonly IDrawerRepository _drawerRepo;

    public DrawerContainerService(IDrawerContainerRepository containerRepo, IDrawerRepository drawerRepo)
    {
        _containerRepo = containerRepo;
        _drawerRepo = drawerRepo;
    }

    public Task<IEnumerable<DrawerContainer>> GetAllAsync() => _containerRepo.GetAllAsync();
    public Task<DrawerContainer?> GetByIdAsync(Guid id) => _containerRepo.GetByIdAsync(id);
    public Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id) => _containerRepo.GetByIdWithDrawersAsync(id);
    public Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer) => _containerRepo.CreateAsync(drawerContainer);
    public Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer) => _containerRepo.UpdateAsync(drawerContainer);
    public Task<bool> DeleteAsync(Guid id) => _containerRepo.DeleteAsync(id);

    public async Task<Drawer?> AddDrawerAsync(Guid containerId, Drawer drawer)
    {
        var container = await _containerRepo.GetByIdAsync(containerId);
        if (container is null) return null;
        return await _drawerRepo.CreateAsync(drawer);
    }
}
