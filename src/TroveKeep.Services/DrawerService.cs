using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerService : IDrawerService
{
    private readonly IDrawerRepository _repo;
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IAllocationRepository _allocationRepo;

    public DrawerService(IDrawerRepository repo, IBulkPieceRepository pieceRepo, IAllocationRepository allocationRepo)
    {
        _repo = repo;
        _pieceRepo = pieceRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<Drawer?> GetByIdAsync(Guid id)
    {
        var drawer = await _repo.GetByIdAsync(id);
        if (drawer is null) return null;
        await EnrichWithCountAsync(drawer);
        return drawer;
    }

    public async Task<Drawer?> GetByIdWithContentsAsync(Guid id)
    {
        var drawer = await _repo.GetByIdAsync(id);
        if (drawer is null) return null;

        var allocs = (await _allocationRepo.GetByStorageAsync(id)).ToList();
        var pieceIds = allocs.Select(a => a.ItemId).ToList();
        var pieces = (await _pieceRepo.GetByIdsAsync(pieceIds)).ToList();

        var allocByItemId = allocs.ToDictionary(a => a.ItemId);
        foreach (var piece in pieces)
            if (allocByItemId.TryGetValue(piece.Id, out var a))
                piece.StorageAllocations = [a];

        drawer.BulkPieces = pieces;
        return drawer;
    }

    public async Task<Drawer?> UpdateAsync(Drawer drawer)
    {
        // DrawerContainerId is not provided by the controller — preserve it from the existing record
        var existing = await _repo.GetByIdAsync(drawer.Id);
        if (existing is null) return null;
        drawer.DrawerContainerId = existing.DrawerContainerId;
        var updated = await _repo.UpdateAsync(drawer);
        if (updated is null) return null;
        await EnrichWithCountAsync(updated);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _allocationRepo.RemoveAllByStorageAsync(id);
        return await _repo.DeleteAsync(id);
    }

    private async Task EnrichWithCountAsync(Drawer drawer)
    {
        var allocs = (await _allocationRepo.GetByStorageAsync(drawer.Id)).ToList();
        drawer.BulkPieces = Enumerable.Repeat(new BulkPiece(), allocs.Count).ToList();
    }
}
