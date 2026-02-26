using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerService : IDrawerService
{
    private readonly IDrawerRepository _repo;

    public DrawerService(IDrawerRepository repo)
    {
        _repo = repo;
    }

    public Task<Drawer?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
    public Task<Drawer?> GetByIdWithContentsAsync(Guid id) => _repo.GetByIdWithContentsAsync(id);
    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);

    public async Task<Drawer?> UpdateAsync(Drawer drawer)
    {
        // DrawerContainerId is not provided by the controller — preserve it from the existing record
        var existing = await _repo.GetByIdAsync(drawer.Id);
        if (existing is null) return null;
        drawer.DrawerContainerId = existing.DrawerContainerId;
        return await _repo.UpdateAsync(drawer);
    }
}
