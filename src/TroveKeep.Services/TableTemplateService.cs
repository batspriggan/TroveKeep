using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class TableTemplateService : ITableTemplateService
{
    private readonly ITableTemplateRepository _repo;

    public TableTemplateService(ITableTemplateRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<TableTemplate>> GetAllAsync() => _repo.GetAllAsync();
    public Task<TableTemplate?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
    public Task<TableTemplate> CreateAsync(TableTemplate template) => _repo.CreateAsync(template);
    public Task<TableTemplate?> UpdateAsync(TableTemplate template) => _repo.UpdateAsync(template);
    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);
}
