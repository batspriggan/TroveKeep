using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class LegoSetService : ILegoSetService
{
    private readonly ILegoSetRepository _setRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IAllocationRepository _allocationRepo;
    private readonly IBaseplateRepository _baseplateRepo;

    public LegoSetService(ILegoSetRepository setRepo, IBoxRepository boxRepo, IAllocationRepository allocationRepo, IBaseplateRepository baseplateRepo)
    {
        _setRepo = setRepo;
        _boxRepo = boxRepo;
        _allocationRepo = allocationRepo;
        _baseplateRepo = baseplateRepo;
    }

    public async Task<IEnumerable<LegoSet>> GetAllAsync()
    {
        var sets = (await _setRepo.GetAllAsync()).ToList();
        if (sets.Count == 0) return sets;

        var allAllocs = await _allocationRepo.GetByItemsAsync(sets.Select(s => s.Id));
        var allocsByItem = allAllocs.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var set in sets)
            set.StorageAllocations = allocsByItem.GetValueOrDefault(set.Id) ?? [];

        return sets;
    }

    public async Task<(IEnumerable<LegoSet> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var (items, total) = await _setRepo.GetPageAsync(page, pageSize, query);
        var list = items.ToList();
        if (list.Count > 0)
        {
            var allocs = await _allocationRepo.GetByItemsAsync(list.Select(x => x.Id));
            var byItem = allocs.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var item in list)
                item.StorageAllocations = byItem.GetValueOrDefault(item.Id) ?? [];
        }
        return (list, total);
    }

    public async Task<LegoSet?> GetByIdAsync(Guid id)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set is null) return null;
        set.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return set;
    }

    public Task<LegoSet> CreateAsync(LegoSet legoSet) => _setRepo.CreateAsync(legoSet);

    public async Task<LegoSet?> UpdateAsync(LegoSet legoSet)
    {
        var updated = await _setRepo.UpdateAsync(legoSet);
        if (updated is null) return null;
        updated.StorageAllocations = (await _allocationRepo.GetByItemAsync(updated.Id)).ToList();
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _allocationRepo.RemoveAllByItemAsync(id);
        await _baseplateRepo.DeleteByLinkedSetIdAsync(id);
        return await _setRepo.DeleteAsync(id);
    }

    public async Task<LegoSet?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;

        var set = await _setRepo.GetByIdAsync(id);
        if (set is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(id)).ToList();
        var currentlyAllocated = allocs.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > set.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding set quantity {set.Quantity}.");

        await _allocationRepo.AddOrIncrementAsync(id, "Set", boxId, StorageType.Box, quantity);

        set.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return set;
    }

    public async Task<LegoSet?> DeallocateStorageAsync(Guid id, Guid storageId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set is null) return null;

        await _allocationRepo.RemoveByItemAndStorageAsync(id, storageId);
        set.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return set;
    }

    public async Task<LegoSet?> ClearStorageAsync(Guid id)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set is null) return null;

        await _allocationRepo.RemoveAllByItemAsync(id);
        set.StorageAllocations = [];
        return set;
    }

    public Task UpdateImageCachedAsync(Guid id) => _setRepo.UpdateImageCachedAsync(id);
}
