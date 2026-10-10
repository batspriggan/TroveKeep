using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly IMongoCollection<RoomDocument> _rooms;
    private readonly ICollectionScope _scope;

    public RoomRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _rooms = database.GetCollection<RoomDocument>("rooms");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        var docs = await _rooms
            .Find(_scope.ScopedTo<RoomDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _rooms.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Room> CreateAsync(Room room)
    {
        room.CollectionId = _scope.ResolveCreateCollectionId(room.CollectionId);
        var doc = ToDocument(room);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _rooms.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<Room?> UpdateAsync(Room room)
    {
        var scopeFilter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, room.Id));
        var existing = await _rooms.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(room);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;
        // Preserve existing layout, aggregate selections and baseplate layouts
        doc.Layout = existing.Layout;
        doc.AggregateSelections = existing.AggregateSelections;
        doc.AggregateBpLayouts = existing.AggregateBpLayouts;

        var result = await _rooms.ReplaceOneAsync(
            x => x.Id == room.Id && x.Version == room.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Room {room.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    /// <summary>
    /// Writes name, dimensions and the layout, then bumps the version. Unlike
    /// <see cref="UpdateAsync"/> (which deliberately preserves the layout, because a rename or a
    /// resize must not move the tables), this is the "layout is part of the change" path used by
    /// the auto-dimensioning, where the placements have just been shifted server-side.
    /// </summary>
    public async Task<Room?> UpdateWithLayoutAsync(Room room)
    {
        var scopeFilter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, room.Id));
        var existing = await _rooms.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(room);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;
        // The layout comes from the caller (already shifted); the aggregate data is untouched.
        doc.AggregateSelections = existing.AggregateSelections;
        doc.AggregateBpLayouts = existing.AggregateBpLayouts;

        var result = await _rooms.ReplaceOneAsync(
            x => x.Id == room.Id && x.Version == room.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Room {room.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<Room?> SaveLayoutAsync(Guid id, IEnumerable<PlacedTable> layout, IEnumerable<AggregateSelection> aggregateSelections, int expectedVersion)
    {
        var layoutDocs = layout.Select(p => new PlacedTableDocument
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
            Rotation = p.Rotation,
        }).ToList();

        var selectionDocs = aggregateSelections.Select(s => new AggregateSelectionDocument
        {
            RepresentativeId = s.RepresentativeId,
            BpKey = s.BpKey,
        }).ToList();

        var update = Builders<RoomDocument>.Update
            .Set(r => r.Layout, layoutDocs)
            .Set(r => r.AggregateSelections, selectionDocs)
            .Set(r => r.UpdatedAt, DateTime.UtcNow)
            .Inc(r => r.Version, 1);

        var result = await _rooms.FindOneAndUpdateAsync(
            _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
                Builders<RoomDocument>.Filter.Where(x => x.Id == id && x.Version == expectedVersion)),
            update,
            new FindOneAndUpdateOptions<RoomDocument> { ReturnDocument = ReturnDocument.After });

        if (result is null)
        {
            // Check if room exists at all; if yes it's a concurrency conflict
            var exists = await _rooms
                .Find(_scope.ScopedTo<RoomDocument>(d => d.CollectionId,
                    Builders<RoomDocument>.Filter.Eq(x => x.Id, id)))
                .AnyAsync();
            if (exists)
                throw new ConcurrencyException($"Room {id} layout was saved by someone else. Please refresh and try again.");
            return null;
        }

        return ToModel(result);
    }

    public async Task<Room?> SaveAggregateBpLayoutAsync(Guid id, string representativeId, IEnumerable<PlacedBaseplate> placedBaseplates)
    {
        var scopeFilter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, id));
        var existing = await _rooms.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var previous = existing.AggregateBpLayouts.FirstOrDefault(l => l.RepresentativeId == representativeId);
        var layouts = existing.AggregateBpLayouts
            .Where(l => l.RepresentativeId != representativeId)
            .ToList();

        layouts.Add(new AggregateBpLayoutDocument
        {
            RepresentativeId = representativeId,
            LayoutVersion = previous?.LayoutVersion ?? 1,
            PlacedBaseplates = placedBaseplates.Select(ToPlacedBaseplateDocument).ToList(),
        });

        var update = Builders<RoomDocument>.Update
            .Set(r => r.AggregateBpLayouts, layouts)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await _rooms.FindOneAndUpdateAsync(
            x => x.Id == id,
            update,
            new FindOneAndUpdateOptions<RoomDocument> { ReturnDocument = ReturnDocument.After });

        return result is null ? null : ToModel(result);
    }

    public async Task<bool> SaveAggregateBpLayoutsAsync(Guid id, IEnumerable<AggregateBpLayout> layouts)
    {
        var docs = layouts.Select(l => new AggregateBpLayoutDocument
        {
            RepresentativeId = l.RepresentativeId,
            LayoutVersion = l.LayoutVersion,
            PlacedBaseplates = l.PlacedBaseplates.Select(ToPlacedBaseplateDocument).ToList(),
        }).ToList();

        var update = Builders<RoomDocument>.Update
            .Set(r => r.AggregateBpLayouts, docs)
            .Set(r => r.UpdatedAt, DateTime.UtcNow)
            .Inc(r => r.Version, 1);

        var filter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, id));
        var result = await _rooms.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task RemoveBaseplateReferencesAsync(Guid baseplateId)
    {
        var rooms = await _rooms.Find(_scope.ScopedTo<RoomDocument>(d => d.CollectionId)).ToListAsync();

        foreach (var room in rooms)
        {
            var changed = false;

            foreach (var layout in room.AggregateBpLayouts)
            {
                var removed = layout.PlacedBaseplates.RemoveAll(p => p.BaseplateId == baseplateId);
                if (removed > 0) changed = true;
            }

            if (!changed) continue;

            var update = Builders<RoomDocument>.Update
                .Set(r => r.AggregateBpLayouts, room.AggregateBpLayouts)
                .Set(r => r.UpdatedAt, DateTime.UtcNow)
                .Inc(r => r.Version, 1);
            await _rooms.UpdateOneAsync(r => r.Id == room.Id, update);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filter = _scope.ScopedTo<RoomDocument>(d => d.CollectionId,
            Builders<RoomDocument>.Filter.Eq(x => x.Id, id));
        var result = await _rooms.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    private static Room ToModel(RoomDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        Name = doc.Name,
        WidthCm = doc.WidthCm,
        DepthCm = doc.DepthCm,
        Obsolete = doc.Obsolete,
        Layout = doc.Layout.Select(p => new PlacedTable
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
            Rotation = p.Rotation,
        }).ToList(),
        AggregateSelections = doc.AggregateSelections.Select(s => new AggregateSelection
        {
            RepresentativeId = s.RepresentativeId,
            BpKey = s.BpKey,
        }).ToList(),
        AggregateBpLayouts = doc.AggregateBpLayouts.Select(l => new AggregateBpLayout
        {
            RepresentativeId = l.RepresentativeId,
            LayoutVersion = l.LayoutVersion,
            PlacedBaseplates = l.PlacedBaseplates.Select(p => new PlacedBaseplate
            {
                InstanceId = p.InstanceId,
                BaseplateId = p.BaseplateId,
                XMm = p.XMm,
                YMm = p.YMm,
                Rotation = p.Rotation,
                SourceSetId = p.SourceSetId,
                PlacementId = p.PlacementId,
            }).ToList(),
        }).ToList(),
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static RoomDocument ToDocument(Room model) => new()
    {
        Id = model.Id,
        CollectionId = model.CollectionId,
        Name = model.Name,
        WidthCm = model.WidthCm,
        DepthCm = model.DepthCm,
        Obsolete = model.Obsolete,
        Layout = model.Layout.Select(p => new PlacedTableDocument
        {
            InstanceId = p.InstanceId,
            TemplateId = p.TemplateId,
            XCm = p.XCm,
            YCm = p.YCm,
            Rotation = p.Rotation,
        }).ToList(),
        AggregateSelections = model.AggregateSelections.Select(s => new AggregateSelectionDocument
        {
            RepresentativeId = s.RepresentativeId,
            BpKey = s.BpKey,
        }).ToList(),
        AggregateBpLayouts = model.AggregateBpLayouts.Select(l => new AggregateBpLayoutDocument
        {
            RepresentativeId = l.RepresentativeId,
            LayoutVersion = l.LayoutVersion,
            PlacedBaseplates = l.PlacedBaseplates.Select(ToPlacedBaseplateDocument).ToList(),
        }).ToList(),
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };

    private static PlacedBaseplateDocument ToPlacedBaseplateDocument(PlacedBaseplate p) => new()
    {
        InstanceId = p.InstanceId,
        BaseplateId = p.BaseplateId,
        XMm = p.XMm,
        YMm = p.YMm,
        Rotation = p.Rotation,
        SourceSetId = p.SourceSetId,
        PlacementId = p.PlacementId,
    };
}
