using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class SearchService : ISearchService
{
    private readonly ILegoSetRepository _setRepo;
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerContainerRepository _containerRepo;
    private readonly IAllocationRepository _allocationRepo;

    public SearchService(
        ILegoSetRepository setRepo,
        IBulkPieceRepository pieceRepo,
        IBoxRepository boxRepo,
        IDrawerContainerRepository containerRepo,
        IAllocationRepository allocationRepo)
    {
        _setRepo = setRepo;
        _pieceRepo = pieceRepo;
        _boxRepo = boxRepo;
        _containerRepo = containerRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<SearchResult> SearchAsync(string query)
    {
        var setsTask = _setRepo.SearchAsync(query);
        var piecesTask = _pieceRepo.SearchAsync(query);
        await Task.WhenAll(setsTask, piecesTask);

        var sets = (await setsTask).ToList();
        var pieces = (await piecesTask).ToList();

        // Batch fetch allocations for all found items
        var allItemIds = sets.Select(s => s.Id).Concat(pieces.Select(p => p.Id)).ToList();
        var allAllocs = allItemIds.Count > 0
            ? (await _allocationRepo.GetByItemsAsync(allItemIds)).ToList()
            : [];

        var allocsByItem = allAllocs.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var set in sets)
            set.StorageAllocations = allocsByItem.GetValueOrDefault(set.Id) ?? [];
        foreach (var piece in pieces)
            piece.StorageAllocations = allocsByItem.GetValueOrDefault(piece.Id) ?? [];

        // Resolve storage names
        var boxIds = new HashSet<Guid>();
        var containerIds = new HashSet<Guid>();
        foreach (var alloc in allAllocs)
        {
            if (alloc.StorageType == StorageType.Box) boxIds.Add(alloc.StorageId);
            else containerIds.Add(alloc.StorageId);
        }

        var boxesTask = _boxRepo.GetByIdsAsync(boxIds);
        var containersTask = _containerRepo.GetByIdsAsync(containerIds);
        await Task.WhenAll(boxesTask, containersTask);

        var boxes = (await boxesTask).ToDictionary(b => b.Id);
        var containers = (await containersTask).ToDictionary(c => c.Id);

        return new SearchResult
        {
            Sets = sets.Select(s => new SetSearchResult
            {
                Id = s.Id,
                SetNumber = s.SetNumber,
                Description = s.Description,
                Quantity = s.Quantity,
                Allocations = s.StorageAllocations
                    .Select(a => ResolveAllocation(a, boxes, containers))
                    .ToList(),
            }),
            Pieces = pieces.Select(p => new PieceSearchResult
            {
                Id = p.Id,
                LegoId = p.LegoId,
                LegoColorId = p.LegoColorId,
                Description = p.Description,
                Quantity = p.Quantity,
                Allocations = p.StorageAllocations
                    .Select(a => ResolveAllocation(a, boxes, containers))
                    .ToList(),
            }),
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
        else
        {
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
}
