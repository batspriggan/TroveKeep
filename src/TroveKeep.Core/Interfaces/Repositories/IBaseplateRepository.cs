using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBaseplateRepository
{
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task DeleteAsync(Guid id);
}
