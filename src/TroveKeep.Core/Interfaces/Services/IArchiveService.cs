using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IArchiveService
{
    Task<(int count, DateTimeOffset importedAt)> ImportColorsAsync(Stream stream);
    Task<(int count, DateTimeOffset? importedAt)> GetColorsStatusAsync();
    Task<IEnumerable<RebrickableColor>> GetColorsAsync();

    Task<(int count, DateTimeOffset importedAt)> ImportSetsAsync(Stream stream);
    Task<(int count, DateTimeOffset? importedAt)> GetSetsStatusAsync();
    Task<IEnumerable<RebrickableSet>> SearchSetsAsync(string query, int limit);

    Task<(int count, DateTimeOffset importedAt)> ImportPartsAsync(Stream stream);
    Task<(int count, DateTimeOffset? importedAt)> GetPartsStatusAsync();
    Task<IEnumerable<RebrickablePart>> SearchPartsAsync(string query, int limit);

    Task<(int count, DateTimeOffset importedAt)> ImportPartsInventoryAsync(Stream stream);
    Task<(int count, DateTimeOffset? importedAt)> GetPartsInventoryStatusAsync();
    Task<IEnumerable<RebrickablePartInventory>> SearchPartsInventoryAsync(string query, int limit);

    Task<(int count, DateTimeOffset importedAt)> ImportPartCategoriesAsync(Stream stream);
    Task<(int count, DateTimeOffset? importedAt)> GetPartCategoriesStatusAsync();
    Task<IEnumerable<RebrickablePartCategory>> GetPartCategoriesAsync();
}
