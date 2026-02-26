using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class LegoSetService : ILegoSetService
{
    private readonly ILegoSetRepository _setRepo;
    private readonly IBoxRepository _boxRepo;

    public LegoSetService(ILegoSetRepository setRepo, IBoxRepository boxRepo)
    {
        _setRepo = setRepo;
        _boxRepo = boxRepo;
    }

    public Task<IEnumerable<LegoSet>> GetAllAsync() => _setRepo.GetAllAsync();
    public Task<LegoSet?> GetByIdAsync(Guid id) => _setRepo.GetByIdAsync(id);
    public Task<LegoSet> CreateAsync(LegoSet legoSet) => _setRepo.CreateAsync(legoSet);
    public Task<LegoSet?> UpdateAsync(LegoSet legoSet) => _setRepo.UpdateAsync(legoSet);
    public Task<bool> DeleteAsync(Guid id) => _setRepo.DeleteAsync(id);

    public async Task<StorageLocation?> GetStorageAsync(Guid id)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set?.BoxId is null) return null;

        var box = await _boxRepo.GetByIdAsync(set.BoxId.Value);
        if (box is null) return null;

        return new StorageLocation
        {
            Type = StorageType.Box,
            StorageId = box.Id,
            StorageName = box.Name,
        };
    }

    public async Task<LegoSet?> AssignToBoxAsync(Guid id, Guid boxId)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;
        return await _setRepo.AssignToBoxAsync(id, boxId);
    }

    public async Task<bool> RemoveStorageAsync(Guid id)
    {
        var result = await _setRepo.RemoveStorageAsync(id);
        return result is not null;
    }
}
