using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IDrawerService
{
    Task<Drawer?> GetByPositionAsync(Guid containerId, int position);
    Task<Drawer?> GetByPositionWithContentsAsync(Guid containerId, int position);
    Task<Drawer?> UpdateAsync(Drawer drawer);
    Task<bool> DeleteAsync(Guid containerId, int position);
    /// <summary>Removes all allocations pointing at a single drawer (the drawer itself stays).</summary>
    Task<bool> EmptyAsync(Guid containerId, int position);
    /// <summary>Moves every allocation from one drawer to another (returns false if source missing).</summary>
    Task<bool> MoveAsync(Guid srcContainerId, int srcPosition, Guid dstContainerId, int dstPosition);
}
