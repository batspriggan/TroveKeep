using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBaseplateRepository
{
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate?> GetByIdAsync(Guid id);
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task UpdateImageCachedAsync(Guid id, bool cached);
    Task DeleteAsync(Guid id);
    Task DeleteByLinkedSetIdAsync(Guid setId);
    Task<Baseplate?> UpdateAsync(Baseplate baseplate);
    Task<Baseplate?> AddOrIncrementReservationAsync(Guid baseplateId, Guid setId, int quantity);
    Task<bool> RemoveReservationAsync(Guid baseplateId, Guid setId);
    Task RemoveReservationsBySetIdAsync(Guid setId);
}
