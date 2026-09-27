using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class RoomService : IRoomService
{
    /// <summary>Smallest room the UI may set (matches the create form's 1 m lower bound).</summary>
    private const int MinRoomCm = 100;

    private readonly IRoomRepository _repo;
    private readonly ITableTemplateRepository _templateRepo;

    public RoomService(IRoomRepository repo, ITableTemplateRepository templateRepo)
    {
        _repo = repo;
        _templateRepo = templateRepo;
    }

    public Task<IEnumerable<Room>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Room?> GetByIdAsync(Guid id) => _repo.GetByIdAsync(id);

    public async Task<Room> CreateAsync(Room room)
    {
        Normalize(room);
        return await _repo.CreateAsync(room);
    }

    public async Task<Room?> UpdateAsync(Room room)
    {
        var existing = await _repo.GetByIdAsync(room.Id);
        if (existing is null) return null;

        Normalize(room);

        // Shrinking a room below the tables' footprint would leave placements outside the walls.
        // The UI blocks the input with the same rule; this is the authoritative check, since the
        // client can be bypassed and the write would otherwise be silently inconsistent.
        var minimum = await ComputeMinimumAsync(existing.Layout);
        if (room.WidthCm < minimum.WidthCm || room.DepthCm < minimum.DepthCm)
        {
            throw new InvalidOperationException(
                $"Room is too small for its tables: the layout needs at least " +
                $"{minimum.WidthCm} x {minimum.DepthCm} cm " +
                $"({minimum.WidthCm / 100.0:0.00} x {minimum.DepthCm / 100.0:0.00} m). " +
                "Move the tables or keep a larger size.");
        }

        return await _repo.UpdateAsync(room);
    }

    /// <summary>
    /// Smallest room that still contains every placed table, plus the layout bounding box.
    /// An empty layout only has to respect <see cref="MinRoomCm"/>.
    /// </summary>
    public async Task<RoomBounds> ComputeMinimumAsync(IEnumerable<PlacedTable> layout)
    {
        var tables = layout.ToList();
        if (tables.Count == 0)
            return new RoomBounds(MinRoomCm, MinRoomCm, 0, 0);

        var templates = (await _templateRepo.GetAllAsync()).ToDictionary(t => t.Id);

        double maxX = 0, maxY = 0;
        foreach (var placed in tables)
        {
            if (!templates.TryGetValue(placed.TemplateId, out var template))
                continue; // a deleted template leaves a placement we cannot measure: ignore it

            // A 90°/270° placement swaps the template's footprint.
            var rotated = NormalizeRotation(placed.Rotation) is 90 or 270;
            var width = rotated ? template.DepthCm : template.WidthCm;
            var depth = rotated ? template.WidthCm : template.DepthCm;

            maxX = Math.Max(maxX, placed.XCm + width);
            maxY = Math.Max(maxY, placed.YCm + depth);
        }

        // Ceiling: a room dimension is an int, and the tables must fit inside it.
        return new RoomBounds(
            Math.Max(MinRoomCm, (int)Math.Ceiling(maxX)),
            Math.Max(MinRoomCm, (int)Math.Ceiling(maxY)),
            0,
            0);
    }

    /// <summary>
    /// Moves every table so the layout's bounding box starts at the origin, and returns the
    /// exact footprint. The room is then resized to it ("fit to tables"), with no margin.
    /// </summary>
    public async Task<Room?> FitToLayoutAsync(Guid id, int expectedVersion)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return null;

        if (existing.Layout.Count == 0)
        {
            throw new InvalidOperationException("There is nothing to fit: the room has no tables.");
        }

        var templates = (await _templateRepo.GetAllAsync()).ToDictionary(t => t.Id);

        double minX = double.MaxValue, minY = double.MaxValue, maxX = 0, maxY = 0;
        foreach (var placed in existing.Layout)
        {
            if (!templates.TryGetValue(placed.TemplateId, out var template)) continue;

            var rotated = NormalizeRotation(placed.Rotation) is 90 or 270;
            var tableW = rotated ? template.DepthCm : template.WidthCm;
            var tableD = rotated ? template.WidthCm : template.DepthCm;

            minX = Math.Min(minX, placed.XCm);
            minY = Math.Min(minY, placed.YCm);
            maxX = Math.Max(maxX, placed.XCm + tableW);
            maxY = Math.Max(maxY, placed.YCm + tableD);
        }

        if (minX == double.MaxValue)
            throw new InvalidOperationException(
                "There is nothing to fit: none of the placed tables still have a template.");

        // Shift to the origin, rounded to whole centimetres so the stored layout stays tidy.
        foreach (var placed in existing.Layout)
        {
            placed.XCm = Math.Round(placed.XCm - minX, 2);
            placed.YCm = Math.Round(placed.YCm - minY, 2);
        }

        var width = Math.Max(MinRoomCm, (int)Math.Ceiling(maxX - minX));
        var depth = Math.Max(MinRoomCm, (int)Math.Ceiling(maxY - minY));

        existing.WidthCm = width;
        existing.DepthCm = depth;
        existing.Version = expectedVersion;

        // Must be UpdateWithLayoutAsync: the plain UpdateAsync exists to preserve the stored
        // layout (so a rename or resize never moves tables), which would throw away the shift
        // applied just above and leave the tables where they were.
        return await _repo.UpdateWithLayoutAsync(existing);
    }

    /// <summary>
    /// Copies a room (dimensions, layout, aggregate selections and baseplate layouts) into a new
    /// one. Fresh instance ids are generated for the placements: they are per-room identities, and
    /// reusing them would make two rooms share placement keys. Reservations are untouched, since
    /// they live on the baseplates, not on the room.
    /// </summary>
    public async Task<Room?> DuplicateAsync(Guid id, string? name)
    {
        var source = await _repo.GetByIdAsync(id);
        if (source is null) return null;

        var copy = new Room
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Copy of {source.Name}" : name.Trim(),
            WidthCm = source.WidthCm,
            DepthCm = source.DepthCm,
            Obsolete = false,
            Layout = source.Layout.Select(p => new PlacedTable
            {
                InstanceId = Guid.NewGuid(),
                TemplateId = p.TemplateId,
                XCm = p.XCm,
                YCm = p.YCm,
                Rotation = p.Rotation,
            }).ToList(),
            AggregateSelections = source.AggregateSelections.Select(s => new AggregateSelection
            {
                RepresentativeId = s.RepresentativeId,
                BpKey = s.BpKey,
            }).ToList(),
            AggregateBpLayouts = source.AggregateBpLayouts.Select(l => new AggregateBpLayout
            {
                RepresentativeId = l.RepresentativeId,
                LayoutVersion = l.LayoutVersion,
                PlacedBaseplates = l.PlacedBaseplates.Select(p => new PlacedBaseplate
                {
                    InstanceId = Guid.NewGuid(),
                    BaseplateId = p.BaseplateId,
                    XMm = p.XMm,
                    YMm = p.YMm,
                    Rotation = p.Rotation,
                    SourceSetId = p.SourceSetId,
                    PlacementId = p.PlacementId,
                }).ToList(),
            }).ToList(),
        };

        return await _repo.CreateAsync(copy);
    }

    /// <summary>Archives or unarchives a room. Archived rooms are hidden from the list.</summary>
    public async Task<Room?> SetObsoleteAsync(Guid id, bool obsolete, int expectedVersion)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing is null) return null;

        existing.Obsolete = obsolete;
        existing.Version = expectedVersion;
        return await _repo.UpdateAsync(existing);
    }

    public Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion) => _repo.SaveLayoutAsync(id, layout, aggregateSelections, expectedVersion);
    public Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates) => _repo.SaveAggregateBpLayoutAsync(id, representativeId, placedBaseplates);
    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);

    private static void Normalize(Room room)
    {
        room.Layout ??= [];
        room.AggregateSelections ??= [];
        room.AggregateBpLayouts ??= [];
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = rotation % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
