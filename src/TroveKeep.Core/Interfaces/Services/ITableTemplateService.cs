using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ITableTemplateService
{
    Task<IEnumerable<TableTemplate>> GetAllAsync();
    Task<TableTemplate?> GetByIdAsync(Guid id);
    Task<TableTemplate> CreateAsync(TableTemplate template);
    Task<TableTemplate?> UpdateAsync(TableTemplate template);
    Task<bool> DeleteAsync(Guid id);
}
