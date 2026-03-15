using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ILegoSetRepository
{
    Task<IEnumerable<LegoSet>> GetAllAsync();
    Task<(IEnumerable<LegoSet> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null);
    Task<LegoSet?> GetByIdAsync(Guid id);
    Task<IEnumerable<LegoSet>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<LegoSet> CreateAsync(LegoSet legoSet);
    Task<LegoSet?> UpdateAsync(LegoSet legoSet);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<LegoSet>> SearchAsync(string query);
    Task UpdateImageCachedAsync(Guid id);
}
