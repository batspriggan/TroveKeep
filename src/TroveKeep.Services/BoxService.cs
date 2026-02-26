using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BoxService : IBoxService
{
    private readonly IBoxRepository _repo;

    public BoxService(IBoxRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<Box>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Box?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);
    public Task<Box?> GetByIdWithContentsAsync(Guid id) => _repo.GetByIdWithContentsAsync(id);
    public Task<Box> CreateAsync(Box box) => _repo.CreateAsync(box);
    public Task<Box?> UpdateAsync(Box box) => _repo.UpdateAsync(box);
    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);
}
