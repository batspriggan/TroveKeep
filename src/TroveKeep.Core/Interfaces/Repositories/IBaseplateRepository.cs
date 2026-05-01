using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBaseplateRepository
{
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate?> GetByIdAsync(Guid id);
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task UpdateImageCachedAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task DeleteByLinkedSetIdAsync(Guid setId);
}
