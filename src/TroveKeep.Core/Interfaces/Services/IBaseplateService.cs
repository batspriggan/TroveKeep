using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IBaseplateService
{
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task DeleteAsync(Guid id);
    (int studX, int studY) GuessStudDimensions(string partDescription);
}
