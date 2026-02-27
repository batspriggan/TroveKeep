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

    public async Task<LegoSet?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;

        var set = await _setRepo.GetByIdAsync(id);
        if (set is null) return null;

        var currentlyAllocated = set.StorageAllocations.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > set.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding set quantity {set.Quantity}.");

        return await _setRepo.AddStorageAsync(id, boxId, StorageType.Box, quantity);
    }

    public Task<LegoSet?> DeallocateStorageAsync(Guid id, Guid storageId) =>
        _setRepo.RemoveStorageAsync(id, storageId);

    public Task<LegoSet?> ClearStorageAsync(Guid id) =>
        _setRepo.ClearStorageAsync(id);
}
