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

        var containerIds = containers.Select(c => c.Id).ToList();
        var allAllocs = (await _allocationRepo.GetByStoragesAsync(containerIds)).ToList();
        var allocsByDrawer = allAllocs
            .GroupBy(a => (a.StorageId, a.StoragePosition))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var container in containers)
            foreach (var drawer in container.Drawers)
            {
                var count = allocsByDrawer.GetValueOrDefault((container.Id, (int?)drawer.Position));
                drawer.BulkPieces = Enumerable.Repeat(new BulkPiece(), count).ToList();
            }

        return containers;
    }

    public Task<DrawerContainer?> GetByIdAsync(Guid id) => _containerRepo.GetByIdAsync(id);

    public async Task<DrawerContainer?> GetByIdWithDrawersAsync(Guid id)
    {
        var container = await _containerRepo.GetByIdAsync(id);
        if (container is null) return null;
        if (container.Drawers.Count == 0) return container;

        var allAllocs = (await _allocationRepo.GetByStorageAsync(id)).ToList();
        var allocsByPosition = allAllocs
            .Where(a => a.StoragePosition.HasValue)
            .GroupBy(a => a.StoragePosition!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allPieceIds = allAllocs.Select(a => a.ItemId).Distinct().ToList();
        var pieces = allPieceIds.Count > 0
            ? (await _pieceRepo.GetByIdsAsync(allPieceIds)).ToDictionary(p => p.Id)
            : new Dictionary<Guid, BulkPiece>();

        foreach (var drawer in container.Drawers)
        {
            var drawerAllocs = allocsByPosition.GetValueOrDefault(drawer.Position) ?? [];
            drawer.BulkPieces = drawerAllocs
                .Select(a => pieces.GetValueOrDefault(a.ItemId))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();
        }

        return container;
    }

    public Task<DrawerContainer> CreateAsync(string name, string? description, int drawerCount)
    {
        var container = new DrawerContainer
        {
            Name = name,
            Description = description,
            Drawers = Enumerable.Range(1, drawerCount)
                .Select(i => new Drawer { Position = i })
                .ToList()
        };
        return _containerRepo.CreateAsync(container);
    }

    public Task<DrawerContainer?> UpdateAsync(DrawerContainer drawerContainer)
        => _containerRepo.UpdateAsync(drawerContainer);

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _allocationRepo.RemoveAllByStorageAsync(id);
        return await _containerRepo.DeleteAsync(id);
    }

    public async Task<Drawer?> AddDrawerAsync(Guid containerId, Drawer drawer)
    {
        var container = await _containerRepo.GetByIdAsync(containerId);
        if (container is null) return null;
        if(container.Drawers.Any(d => d.Position == drawer.Position)) return null;
        return await _drawerRepo.CreateAsync(drawer);
    }
}
