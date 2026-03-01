using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> CreateAsync(Room room);
    Task<Room?> UpdateAsync(Room room);
    Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout);
    Task<bool> DeleteAsync(Guid id);
}
