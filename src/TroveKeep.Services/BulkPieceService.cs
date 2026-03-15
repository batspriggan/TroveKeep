using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BulkPieceService : IBulkPieceService
{
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerRepository _drawerRepo;
    private readonly IAllocationRepository _allocationRepo;

    public BulkPieceService(
        IBulkPieceRepository pieceRepo,
        IBoxRepository boxRepo,
        IDrawerRepository drawerRepo,
        IAllocationRepository allocationRepo)
    {
        _pieceRepo = pieceRepo;
        _boxRepo = boxRepo;
        _drawerRepo = drawerRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<IEnumerable<BulkPiece>> GetAllAsync()
    {
        var pieces = (await _pieceRepo.GetAllAsync()).ToList();
        if (pieces.Count == 0) return pieces;

        var allAllocs = await _allocationRepo.GetByItemsAsync(pieces.Select(p => p.Id));
        var allocsByItem = allAllocs.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var piece in pieces)
            piece.StorageAllocations = allocsByItem.GetValueOrDefault(piece.Id) ?? [];

        return pieces;
    }

    public async Task<(IEnumerable<BulkPiece> Items, long Total)> GetPageAsync(int page, int pageSize, string? query = null)
    {
        var (items, total) = await _pieceRepo.GetPageAsync(page, pageSize, query);
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

    public async Task<BulkPiece?> GetByIdAsync(Guid id)
    {
        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;
        piece.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return piece;
    }

    public Task<BulkPiece> CreateAsync(BulkPiece bulkPiece) => _pieceRepo.CreateAsync(bulkPiece);

    public async Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece)
    {
        var updated = await _pieceRepo.UpdateAsync(bulkPiece);
        if (updated is null) return null;
        updated.StorageAllocations = (await _allocationRepo.GetByItemAsync(updated.Id)).ToList();
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _allocationRepo.RemoveAllByItemAsync(id);
        return await _pieceRepo.DeleteAsync(id);
    }

    public async Task<BulkPiece?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;

        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(id)).ToList();
        var currentlyAllocated = allocs.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > piece.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding piece quantity {piece.Quantity}.");

        await _allocationRepo.AddOrIncrementAsync(id, "Piece", boxId, StorageType.Box, quantity);

        piece.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return piece;
    }

    public async Task<BulkPiece?> AllocateToDrawerAsync(Guid id, Guid containerId, int position, int quantity)
    {
        var drawer = await _drawerRepo.GetByPositionAsync(containerId, position);
        if (drawer is null) return null;

        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(id)).ToList();
        var currentlyAllocated = allocs.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > piece.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding piece quantity {piece.Quantity}.");

        await _allocationRepo.AddOrIncrementAsync(id, "Piece", containerId, StorageType.Drawer, quantity, storagePosition: position);

        piece.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return piece;
    }

    public async Task<BulkPiece?> DeallocateFromBoxAsync(Guid id, Guid boxId)
    {
        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        await _allocationRepo.RemoveByItemAndStorageAsync(id, boxId);
        piece.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return piece;
    }

    public async Task<BulkPiece?> DeallocateFromDrawerAsync(Guid id, Guid containerId, int position)
    {
        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        await _allocationRepo.RemoveByItemAndStorageAsync(id, containerId, position);
        piece.StorageAllocations = (await _allocationRepo.GetByItemAsync(id)).ToList();
        return piece;
    }

    public async Task<BulkPiece?> ClearStorageAsync(Guid id)
    {
        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        await _allocationRepo.RemoveAllByItemAsync(id);
        piece.StorageAllocations = [];
        return piece;
    }

    public Task UpdateImageCachedAsync(Guid id) => _pieceRepo.UpdateImageCachedAsync(id);
}
