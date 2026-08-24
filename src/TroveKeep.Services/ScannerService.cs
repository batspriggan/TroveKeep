using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class ScannerService : IScannerService
{
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IAllocationRepository _allocationRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerContainerRepository _containerRepo;

    public ScannerService(
        IBulkPieceRepository pieceRepo,
        IAllocationRepository allocationRepo,
        IBoxRepository boxRepo,
        IDrawerContainerRepository containerRepo)
    {
        _pieceRepo = pieceRepo;
        _allocationRepo = allocationRepo;
        _boxRepo = boxRepo;
        _containerRepo = containerRepo;
    }

    public async Task<ScannerResult?> ResolvePieceAsync(string legoId, int legoColorId)
    {
        var piece = await _pieceRepo.GetByBusinessKeyAsync(legoId, legoColorId);
        if (piece is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(piece.Id)).ToList();

        var boxIds = new HashSet<Guid>();
        var containerIds = new HashSet<Guid>();
        foreach (var alloc in allocs)
        {
            if (alloc.StorageType == StorageType.Box) boxIds.Add(alloc.StorageId);
            else containerIds.Add(alloc.StorageId);
        }

        var boxesTask = _boxRepo.GetByIdsAsync(boxIds);
        var containersTask = _containerRepo.GetByIdsAsync(containerIds);
        await Task.WhenAll(boxesTask, containersTask);

        var boxes = (await boxesTask).ToDictionary(b => b.Id);
        var containers = (await containersTask).ToDictionary(c => c.Id);

        return new ScannerResult
        {
            Id = piece.Id,
            LegoId = piece.LegoId,
            LegoColorId = piece.LegoColorId,
            Description = piece.Description,
            Quantity = piece.Quantity,
            Allocations = allocs.Select(a => ResolveAllocation(a, boxes, containers)).ToList(),
        };
    }

    private static ResolvedAllocation ResolveAllocation(
        StorageAllocation alloc,
        Dictionary<Guid, Box> boxes,
        Dictionary<Guid, DrawerContainer> containers)
    {
        if (alloc.StorageType == StorageType.Box)
        {
            var box = boxes.GetValueOrDefault(alloc.StorageId);
            return new ResolvedAllocation
            {
                StorageId = alloc.StorageId,
                StorageType = StorageType.Box,
                StorageName = box?.Name ?? "(unknown box)",
                Quantity = alloc.Quantity,
            };
        }

        var container = containers.GetValueOrDefault(alloc.StorageId);
        return new ResolvedAllocation
        {
            StorageId = alloc.StorageId,
            StorageType = StorageType.Drawer,
            StorageName = container is null ? $"(unknown container) Pos {alloc.StoragePosition}"
                : $"{container.Name} Pos {alloc.StoragePosition}",
            DrawerContainerId = alloc.StorageId,
            DrawerContainerName = container?.Name,
            DrawerPosition = alloc.StoragePosition,
            Quantity = alloc.Quantity,
        };
    }
}
