using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class SearchService : ISearchService
{
    private readonly ILegoSetRepository _setRepo;
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IBoxRepository _boxRepo;
    private readonly IDrawerRepository _drawerRepo;
    private readonly IDrawerContainerRepository _containerRepo;

    public SearchService(
        ILegoSetRepository setRepo,
        IBulkPieceRepository pieceRepo,
        IBoxRepository boxRepo,
        IDrawerRepository drawerRepo,
        IDrawerContainerRepository containerRepo)
    {
        _setRepo = setRepo;
        _pieceRepo = pieceRepo;
        _boxRepo = boxRepo;
        _drawerRepo = drawerRepo;
        _containerRepo = containerRepo;
    }

    public async Task<SearchResult> SearchAsync(string query)
    {
        var setsTask = _setRepo.SearchAsync(query);
        var piecesTask = _pieceRepo.SearchAsync(query);
        await Task.WhenAll(setsTask, piecesTask);

        var sets = (await setsTask).ToList();
        var pieces = (await piecesTask).ToList();

        var boxIds = new HashSet<Guid>();
        var drawerIds = new HashSet<Guid>();
        foreach (var alloc in sets.SelectMany(s => s.StorageAllocations)
            .Concat(pieces.SelectMany(p => p.StorageAllocations)))
        {
            if (alloc.Type == StorageType.Box) boxIds.Add(alloc.StorageId);
            else drawerIds.Add(alloc.StorageId);
        }

        var boxesTask = _boxRepo.GetByIdsAsync(boxIds);
        var drawersTask = _drawerRepo.GetByIdsAsync(drawerIds);
        await Task.WhenAll(boxesTask, drawersTask);

        var boxes = (await boxesTask).ToDictionary(b => b.Id);
        var drawers = (await drawersTask).ToDictionary(d => d.Id);

        Dictionary<Guid, DrawerContainer> containers = [];
        if (drawers.Count > 0)
        {
            var containerIds = drawers.Values.Select(d => d.DrawerContainerId).Distinct();
            var fetched = await _containerRepo.GetByIdsAsync(containerIds);
            containers = fetched.ToDictionary(c => c.Id);
        }

        return new SearchResult
        {
            Sets = sets.Select(s => new SetSearchResult
            {
                Id = s.Id,
                SetNumber = s.SetNumber,
                Description = s.Description,
                Quantity = s.Quantity,
                Allocations = s.StorageAllocations
                    .Select(a => ResolveAllocation(a, boxes, drawers, containers))
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
                    .Select(a => ResolveAllocation(a, boxes, drawers, containers))
                    .ToList(),
            }),
        };
    }

    private static ResolvedAllocation ResolveAllocation(
        StorageAllocation alloc,
        Dictionary<Guid, Box> boxes,
        Dictionary<Guid, Drawer> drawers,
        Dictionary<Guid, DrawerContainer> containers)
    {
        if (alloc.Type == StorageType.Box)
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
            var drawer = drawers.GetValueOrDefault(alloc.StorageId);
            var container = drawer is not null
                ? containers.GetValueOrDefault(drawer.DrawerContainerId)
                : null;
            return new ResolvedAllocation
            {
                StorageId = alloc.StorageId,
                StorageType = StorageType.Drawer,
                StorageName = drawer is null ? "(unknown drawer)"
                    : drawer.Label is not null ? drawer.Label
                    : $"Drawer #{drawer.Position}",
                DrawerContainerId = drawer?.DrawerContainerId,
                DrawerContainerName = container?.Name,
                DrawerPosition = drawer?.Position,
                Quantity = alloc.Quantity,
            };
        }
    }
}
