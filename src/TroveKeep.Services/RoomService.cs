using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repo;

    public RoomService(IRoomRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<Room>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Room?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
    public Task<Room> CreateAsync(Room room) => _repo.CreateAsync(room);
    public Task<Room?> UpdateAsync(Room room) => _repo.UpdateAsync(room);
    public Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion) => _repo.SaveLayoutAsync(id, layout, aggregateSelections, expectedVersion);
    public Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates) => _repo.SaveAggregateBpLayoutAsync(id, representativeId, placedBaseplates);
    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);
}
