using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IPartInventoryArchiveRepository
{
    Task<int> ReplaceAllAsync(IEnumerable<RebrickablePartInventory> parts);
    Task<DateTimeOffset?> GetLastImportedAtAsync();
    Task<int> GetCountAsync();
    Task<IEnumerable<RebrickablePartInventory>> SearchAsync(string query, int limit);
    Task<RebrickablePartInventory?> GetByPartNumAsync(string partNum);
}
