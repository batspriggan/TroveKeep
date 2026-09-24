using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> CreateAsync(Room room);
    Task<Room?> UpdateAsync(Room room);
    Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion);
    Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates);

    /// <summary>
    /// Replaces the whole <c>AggregateBpLayouts</c> array of a room (read-modify-write of the
    /// single room document). Every other field is preserved.
    /// </summary>
    Task<bool> SaveAggregateBpLayoutsAsync(Guid id, IEnumerable<AggregateBpLayout> layouts);

    /// <summary>
    /// Removes every <c>PlacedBaseplate</c> that points at <paramref name="baseplateId"/> across
    /// all rooms. Used when a baseplate row is deleted so no room keeps a dangling reference.
    /// </summary>
    Task RemoveBaseplateReferencesAsync(Guid baseplateId);

    Task<bool> DeleteAsync(Guid id);
}
