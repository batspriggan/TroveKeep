using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ITableTemplateRepository
{
    Task<IEnumerable<TableTemplate>> GetAllAsync();
    Task<TableTemplate?> GetByIdAsync(Guid id);
    Task<TableTemplate> CreateAsync(TableTemplate template);
    Task<TableTemplate?> UpdateAsync(TableTemplate template);
    Task<bool> DeleteAsync(Guid id);
}
