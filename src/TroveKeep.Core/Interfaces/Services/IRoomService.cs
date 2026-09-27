using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IRoomService
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room> CreateAsync(Room room);

    /// <summary>
    /// Updates name and dimensions. Throws <see cref="InvalidOperationException"/> when the new
    /// size cannot contain the current layout.
    /// </summary>
    Task<Room?> UpdateAsync(Room room);

    /// <summary>Smallest room size that still contains every placed table.</summary>
    Task<RoomBounds> ComputeMinimumAsync(IEnumerable<PlacedTable> layout);

    /// <summary>Shifts the layout to the origin and resizes the room to its exact footprint.</summary>
    Task<Room?> FitToLayoutAsync(Guid id, int expectedVersion);

    /// <summary>Copies a room, with fresh placement ids, into a new room.</summary>
    Task<Room?> DuplicateAsync(Guid id, string? name);

    /// <summary>Archives or unarchives a room (archived rooms are hidden from the list).</summary>
    Task<Room?> SetObsoleteAsync(Guid id, bool obsolete, int expectedVersion);

    Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion);
    Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates);
    Task<bool> DeleteAsync(Guid id);
}
