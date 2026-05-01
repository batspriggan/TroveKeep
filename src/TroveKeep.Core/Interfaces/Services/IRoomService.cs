using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IRoomService
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> CreateAsync(Room room);
    Task<Room?> UpdateAsync(Room room);
    Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion);
    Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates);
    Task<bool> DeleteAsync(Guid id);
}
