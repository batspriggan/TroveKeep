using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IColorRepository
{
    Task<IEnumerable<RebrickableColor>> GetAllAsync();
    Task<int> ReplaceAllAsync(IEnumerable<RebrickableColor> colors);
    Task<DateTimeOffset?> GetLastImportedAtAsync();
}
