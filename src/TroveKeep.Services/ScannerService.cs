using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class ScannerService : IScannerService
{
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly ILegoSetRepository _setRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerContainerRepository _containerRepo;
    private readonly IAllocationRepository _allocationRepo;

    public ScannerService(
        IBulkPieceRepository pieceRepo,
        ILegoSetRepository setRepo,
        IBoxRepository boxRepo,
        IDrawerContainerRepository containerRepo,
        IAllocationRepository allocationRepo)
    {
        _pieceRepo = pieceRepo;
        _setRepo = setRepo;
        _boxRepo = boxRepo;
        _containerRepo = containerRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<ScannerResult?> ResolveAsync(LabelRef reference) => reference.Kind switch
    {
        LabelRefKind.Piece => await ResolvePieceAsync(reference),
        LabelRefKind.Set => await ResolveSetAsync(reference),
        LabelRefKind.Box => await ResolveBoxAsync(reference),
        _ => null,
    };

    private async Task<ScannerResult?> ResolvePieceAsync(LabelRef reference)
    {
        if (string.IsNullOrWhiteSpace(reference.LegoId) || reference.ColorId is null)
            return null;

        var piece = await _pieceRepo.GetByBusinessKeyAsync(reference.LegoId, reference.ColorId.Value);
        if (piece is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(piece.Id)).ToList();

        return new ScannerResult
        {
            Kind = LabelRefKind.Piece,
            Id = piece.Id,
            Title = piece.LegoId,
            Subtitle = piece.Description,
            ColorId = piece.LegoColorId,
            Quantity = piece.Quantity,
            Allocations = await ResolveAllocationsAsync(allocs),
        };
    }

    private async Task<ScannerResult?> ResolveSetAsync(LabelRef reference)
    {
        if (string.IsNullOrWhiteSpace(reference.SetNumber))
            return null;

        var (items, _) = await _setRepo.GetPageAsync(1, 1, reference.SetNumber);
        var set = items.FirstOrDefault(s => string.Equals(s.SetNumber, reference.SetNumber, StringComparison.Ordinal));
        if (set is null) return null;

        var allocs = (await _allocationRepo.GetByItemAsync(set.Id)).ToList();

        return new ScannerResult
        {
            Kind = LabelRefKind.Set,
            Id = set.Id,
            Title = set.SetNumber,
            Subtitle = set.Description,
            Quantity = set.Quantity,
            Allocations = await ResolveAllocationsAsync(allocs),
        };
    }

    private async Task<ScannerResult?> ResolveBoxAsync(LabelRef reference)
    {
        if (reference.BoxId is null) return null;

        var box = await _boxRepo.GetByIdAsync(reference.BoxId.Value);
        if (box is null) return null;

        return new ScannerResult
        {
            Kind = LabelRefKind.Box,
            Id = box.Id,
            Title = box.Name,
            Quantity = 0,
            Allocations = [],
        };
    }

    private async Task<List<ResolvedAllocation>> ResolveAllocationsAsync(IReadOnlyList<StorageAllocation> allocs)
    {
        var boxIds = new HashSet<Guid>();
        var containerIds = new HashSet<Guid>();
        foreach (var alloc in allocs)
        {
            if (alloc.StorageType == StorageType.Box) boxIds.Add(alloc.StorageId);
            else containerIds.Add(alloc.StorageId);
        }

        var boxesTask = _boxRepo.GetByIdsAsync(boxIds);
        var containersTask = _containerRepo.GetByIdsAsync(containerIds);

        // _containerRepo resolved from DI; both dictionaries built before mapping.
        var boxes = boxIds.Count > 0 ? (await boxesTask).ToDictionary(b => b.Id) : new Dictionary<Guid, Box>();
        var containers = containerIds.Count > 0 ? (await containersTask).ToDictionary(c => c.Id) : new Dictionary<Guid, DrawerContainer>();

        return allocs.Select(a => ResolveAllocation(a, boxes, containers)).ToList();
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
