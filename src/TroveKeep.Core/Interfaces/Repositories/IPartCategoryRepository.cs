using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IPartCategoryRepository
{
    Task<IEnumerable<RebrickablePartCategory>> GetAllAsync();
    Task<int> ReplaceAllAsync(IEnumerable<RebrickablePartCategory> categories);
    Task<DateTimeOffset?> GetLastImportedAtAsync();
}
