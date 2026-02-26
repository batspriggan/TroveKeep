using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BulkPieceService : IBulkPieceService
{
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerRepository _drawerRepo;
    private readonly IDrawerContainerRepository _containerRepo;

    public BulkPieceService(
        IBulkPieceRepository pieceRepo,
        IBoxRepository boxRepo,
        IDrawerRepository drawerRepo,
        IDrawerContainerRepository containerRepo)
    {
        _pieceRepo = pieceRepo;
        _boxRepo = boxRepo;
        _drawerRepo = drawerRepo;
        _containerRepo = containerRepo;
    }

    public Task<IEnumerable<BulkPiece>> GetAllAsync() => _pieceRepo.GetAllAsync();
    public Task<BulkPiece?> GetByIdAsync(Guid id) => _pieceRepo.GetByIdAsync(id);
    public Task<BulkPiece> CreateAsync(BulkPiece bulkPiece) => _pieceRepo.CreateAsync(bulkPiece);
    public Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece) => _pieceRepo.UpdateAsync(bulkPiece);
    public Task<bool> DeleteAsync(Guid id) => _pieceRepo.DeleteAsync(id);

    public async Task<StorageLocation?> GetStorageAsync(Guid id)
    {
        var piece = await _pieceRepo.GetByIdAsync(id);
        if (piece is null) return null;

        if (piece.BoxId.HasValue)
        {
            var box = await _boxRepo.GetByIdAsync(piece.BoxId.Value);
            if (box is null) return null;
            return new StorageLocation
            {
                Type = StorageType.Box,
                StorageId = box.Id,
                StorageName = box.Name,
            };
        }

        if (piece.DrawerId.HasValue)
        {
            var drawer = await _drawerRepo.GetByIdAsync(piece.DrawerId.Value);
            if (drawer is null) return null;
            var container = await _containerRepo.GetByIdAsync(drawer.DrawerContainerId);
            return new StorageLocation
            {
                Type = StorageType.Drawer,
                StorageId = drawer.Id,
                StorageName = drawer.Label ?? $"Position {drawer.Position}",
                DrawerContainerId = container?.Id,
                DrawerContainerName = container?.Name,
                DrawerPosition = drawer.Position,
            };
        }

        return null;
    }

    public async Task<BulkPiece?> AssignToBoxAsync(Guid id, Guid boxId)
    {
        var box = await _boxRepo.GetByIdAsync(boxId);
        if (box is null) return null;
        return await _pieceRepo.AssignToBoxAsync(id, boxId);
    }

    public async Task<BulkPiece?> AssignToDrawerAsync(Guid id, Guid drawerId)
    {
        var drawer = await _drawerRepo.GetByIdAsync(drawerId);
        if (drawer is null) return null;
        return await _pieceRepo.AssignToDrawerAsync(id, drawerId);
    }

    public async Task<bool> RemoveStorageAsync(Guid id)
    {
        var result = await _pieceRepo.RemoveStorageAsync(id);
        return result is not null;
    }
}
