using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IPartArchiveRepository
{
    Task<int> ReplaceAllAsync(IEnumerable<RebrickablePart> parts);
    Task<DateTimeOffset?> GetLastImportedAtAsync();
    Task<int> GetCountAsync();
    Task<IEnumerable<RebrickablePart>> SearchAsync(string query, int limit, int? categoryId = null);
}
