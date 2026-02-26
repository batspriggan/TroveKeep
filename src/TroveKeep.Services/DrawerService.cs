using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerService : IDrawerService
{
    public Task<Drawer?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<Drawer?> GetByIdWithContentsAsync(Guid id) => throw new NotImplementedException();
    public Task<Drawer?> UpdateAsync(Drawer drawer) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
}
