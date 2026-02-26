using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Repositories;

public class DrawerRepository : IDrawerRepository
{
    public Task<Drawer?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<Drawer?> GetByIdWithContentsAsync(Guid id) => throw new NotImplementedException();
    public Task<Drawer> CreateAsync(Drawer drawer) => throw new NotImplementedException();
    public Task<Drawer?> UpdateAsync(Drawer drawer) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
}
