using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BulkPieceService : IBulkPieceService
{
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerRepository _drawerRepo;

    public BulkPieceService(
        IBulkPieceRepository pieceRepo,
        IBoxRepository boxRepo,
        IDrawerRepository drawerRepo)
    {
        _pieceRepo = pieceRepo;
        _boxRepo = boxRepo;
        _drawerRepo = drawerRepo;
    }

    public Task<IEnumerable<BulkPiece>> GetAllAsync() => _pieceRepo.GetAllAsync();
    public Task<BulkPiece?> GetByIdAsync(Guid id) => _pieceRepo.GetByIdAsync(id);
    public Task<BulkPiece> CreateAsync(BulkPiece bulkPiece) => _pieceRepo.CreateAsync(bulkPiece);
    public Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece) => _pieceRepo.UpdateAsync(bulkPiece);
    public Task<bool> DeleteAsync(Guid id) => _pieceRepo.DeleteAsync(id);

    public async Task<BulkPiece?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;

        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        var currentlyAllocated = piece.StorageAllocations.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > piece.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding piece quantity {piece.Quantity}.");

        return await _pieceRepo.AddStorageAsync(id, boxId, StorageType.Box, quantity);
    }

    public async Task<BulkPiece?> AllocateToDrawerAsync(Guid id, Guid drawerId, int quantity)
    {
        var drawer = await _drawerRepo.GetByIdAsync(drawerId);
        if (drawer is null) return null;

        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        var currentlyAllocated = piece.StorageAllocations.Sum(a => a.Quantity);
        if (currentlyAllocated + quantity > piece.Quantity)
            throw new InvalidOperationException(
                $"Cannot allocate {quantity}: total would be {currentlyAllocated + quantity}, exceeding piece quantity {piece.Quantity}.");

        return await _pieceRepo.AddStorageAsync(id, drawerId, StorageType.Drawer, quantity);
    }

    public Task<BulkPiece?> DeallocateStorageAsync(Guid id, Guid storageId) =>
        _pieceRepo.RemoveStorageAsync(id, storageId);

    public Task<BulkPiece?> ClearStorageAsync(Guid id) =>
        _pieceRepo.ClearStorageAsync(id);
}
