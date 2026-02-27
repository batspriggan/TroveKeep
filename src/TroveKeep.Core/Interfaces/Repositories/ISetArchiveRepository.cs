using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ISetArchiveRepository
{
    Task<IEnumerable<RebrickableSet>> GetAllAsync();
    Task<int> ReplaceAllAsync(IEnumerable<RebrickableSet> sets);
    Task<DateTimeOffset?> GetLastImportedAtAsync();
}
