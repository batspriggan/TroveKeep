using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class DrawerContainerService : IDrawerContainerService
{
    private readonly IDrawerContainerRepository _containerRepo;
    private readonly IDrawerRepository _drawerRepo;
    private readonly IBulkPieceRepository _pieceRepo;
    private readonly IAllocationRepository _allocationRepo;

    public DrawerContainerService(
        IDrawerContainerRepository containerRepo,
        IDrawerRepository drawerRepo,
        IBulkPieceRepository pieceRepo,
        IAllocationRepository allocationRepo)
    {
        _containerRepo = containerRepo;
        _drawerRepo = drawerRepo;
        _pieceRepo = pieceRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task<IEnumerable<DrawerContainer>> GetAllAsync()
    {
        var containers = (await _containerRepo.GetAllAsync()).ToList();
        if (containers.Count == 0) return containers;

        var allDrawerIds = containers.SelectMany(c => c.Drawers).Select(d => d.Id).ToList();
        if (allDrawerIds.Count == 0) return containers;

        var allAllocs = (await _allocationRepo.GetByStoragesAsync(allDrawerIds)).ToList();
        var allocsByDrawer = allAllocs.GroupBy(a => a.StorageId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var container in containers)
            foreach (var drawer in container.Drawers)
            {
                var count = allocsByDrawer.GetValueOrDefault(drawer.Id)?.Count ?? 0;
                drawer.BulkPieces = Enumerable.Repeat(new BulkPiece(), count).ToList();
            }

        return containers;
    }

    public Task<DrawerContainer?> GetByIdAsync(Guid id) => _containerRepo.GetByIdAsync(id);

    public async Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id)
    {
        var container = await _containerRepo.GetByIdAsync(id);
        if (container is null) return null;

        var drawerIds = container.Drawers.Select(d => d.Id).ToList();
        if (drawerIds.Count == 0) return container;

        var allAllocs = (await _allocationRepo.GetByStoragesAsync(drawerIds)).ToList();
        var allocsByDrawer = allAllocs.GroupBy(a => a.StorageId).ToDictionary(g => g.Key, g => g.ToList());

        var allPieceIds = allAllocs.Select(a => a.ItemId).Distinct().ToList();
        var pieces = allPieceIds.Count > 0
            ? (await _pieceRepo.GetByIdsAsync(allPieceIds)).ToDictionary(p => p.Id)
            : new Dictionary<Guid, BulkPiece>();

        foreach (var drawer in container.Drawers)
        {
            var drawerAllocs = allocsByDrawer.GetValueOrDefault(drawer.Id) ?? [];
            drawer.BulkPieces = drawerAllocs
                .Select(a => pieces.GetValueOrDefault(a.ItemId))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();
        }

        return container;
    }

    public Task<DrawerContainer> CreateAsync(DrawerContainer drawerContainer)
        => _containerRepo.CreateAsync(drawerContainer);

    public Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer)
        => _containerRepo.UpdateAsync(drawerContainer);

    public async Task<bool> DeleteAsync(Guid id)
    {
        var container = await _containerRepo.GetByIdAsync(id);
        if (container is not null)
            foreach (var drawer in container.Drawers)
                await _allocationRepo.RemoveAllByStorageAsync(drawer.Id);

        return await _containerRepo.DeleteAsync(id);
    }

    public async Task<Drawer?> AddDrawerAsync(Guid containerId, Drawer drawer)
    {
        var container = await _containerRepo.GetByIdAsync(containerId);
        if (container is null) return null;
        return await _drawerRepo.CreateAsync(drawer);
    }
}
