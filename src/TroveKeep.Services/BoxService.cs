using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BoxService : IBoxService
{
    private readonly IBoxRepository _boxRepo;
    private readonly ILegoSetRepository _setRepo;
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IAllocationRepository _allocationRepo;

    public BoxService(
        IBoxRepository boxRepo,
        ILegoSetRepository setRepo,
        IBulkPieceRepository pieceRepo,
        IAllocationRepository allocationRepo)
    {
        _boxRepo = boxRepo;
        _setRepo = setRepo;
        _pieceRepo = pieceRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<IEnumerable<Box>> GetAllAsync()
    {
        var boxes = (await _boxRepo.GetAllAsync()).ToList();
        if (boxes.Count == 0) return boxes;

        var allAllocs = (await _allocationRepo.GetByStoragesAsync(boxes.Select(b => b.Id))).ToList();
        var allocsByBox = allAllocs.GroupBy(a => a.StorageId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var box in boxes)
        {
            var allocs = allocsByBox.GetValueOrDefault(box.Id) ?? [];
            box.Sets = Enumerable.Repeat(new LegoSet(), allocs.Count(a => a.ItemType == "Set")).ToList();
            box.BulkPieces = Enumerable.Repeat(new BulkPiece(), allocs.Count(a => a.ItemType == "Piece")).ToList();
        }
        return boxes;
    }

    public async Task<Box?> GetByIdAsync(Guid id)
    {
        var box = await _boxRepo.GetByIdAsync(id);
        if (box is null) return null;
        await EnrichWithCountsAsync(box);
        return box;
    }

    public async Task<Box?> GetByIdWithContentsAsync(Guid id)
    {
        var box = await _boxRepo.GetByIdAsync(id);
        if (box is null) return null;

        var allocs = (await _allocationRepo.GetByStorageAsync(id)).ToList();

        var setIds = allocs.Where(a => a.ItemType == "Set").Select(a => a.ItemId).ToList();
        var pieceIds = allocs.Where(a => a.ItemType == "Piece").Select(a => a.ItemId).ToList();

        var setsTask = _setRepo.GetByIdsAsync(setIds);
        var piecesTask = _pieceRepo.GetByIdsAsync(pieceIds);
        await Task.WhenAll(setsTask, piecesTask);

        var sets = (await setsTask).ToList();
        var pieces = (await piecesTask).ToList();

        var allocByItemId = allocs.ToDictionary(a => a.ItemId);
        foreach (var set in sets)
            if (allocByItemId.TryGetValue(set.Id, out var a))
                set.StorageAllocations = [a];
        foreach (var piece in pieces)
            if (allocByItemId.TryGetValue(piece.Id, out var a))
                piece.StorageAllocations = [a];

        box.Sets = sets;
        box.BulkPieces = pieces;
        return box;
    }

    public Task<Box> CreateAsync(Box box) => _boxRepo.CreateAsync(box);

    public async Task<Box?> UpdateAsync(Box box)
    {
        var updated = await _boxRepo.UpdateAsync(box);
        if (updated is null) return null;
        await EnrichWithCountsAsync(updated);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _allocationRepo.RemoveAllByStorageAsync(id);
        return await _boxRepo.DeleteAsync(id);
    }

    private async Task EnrichWithCountsAsync(Box box)
    {
        var allocs = (await _allocationRepo.GetByStorageAsync(box.Id)).ToList();
        box.Sets = Enumerable.Repeat(new LegoSet(), allocs.Count(a => a.ItemType == "Set")).ToList();
        box.BulkPieces = Enumerable.Repeat(new BulkPiece(), allocs.Count(a => a.ItemType == "Piece")).ToList();
    }
}
