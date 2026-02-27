using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IArchiveService
{
    Task<(int count, DateTimeOffset importedAt)> ImportColorsAsync();
    Task<(int count, DateTimeOffset? importedAt)> GetColorsStatusAsync();
    Task<IEnumerable<RebrickableColor>> GetColorsAsync();

    Task<(int count, DateTimeOffset importedAt)> ImportSetsAsync();
    Task<(int count, DateTimeOffset? importedAt)> GetSetsStatusAsync();
    Task<IEnumerable<RebrickableSet>> SearchSetsAsync(string query, int limit);
}
