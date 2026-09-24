using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class BaseplateRepository : IBaseplateRepository
{
    private const int MaxReservationRetries = 5;

    private readonly IMongoCollection<BaseplateDocument> _baseplates;

    public BaseplateRepository(IMongoDatabase database)
    {
        _baseplates = database.GetCollection<BaseplateDocument>("baseplates");
    }

    public async Task<IEnumerable<Baseplate>> GetAllAsync()
    {
        var docs = await _baseplates.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<Baseplate?> GetByIdAsync(Guid id)
    {
        var doc = await _baseplates.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<Baseplate> CreateAsync(Baseplate baseplate)
    {
        var doc = ToDocument(baseplate);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _baseplates.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task UpdateImageCachedAsync(Guid id, bool cached)
    {
        var update = Builders<BaseplateDocument>.Update
            .Set(d => d.ImageCached, cached)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _baseplates.UpdateOneAsync(d => d.Id == id, update);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _baseplates.DeleteOneAsync(d => d.Id == id);
    }

    public async Task DeleteByLinkedSetIdAsync(Guid setId)
    {
        await _baseplates.DeleteManyAsync(d => d.LinkedSetId == setId);
    }

    public async Task<Baseplate?> UpdateAsync(Baseplate baseplate)
    {
        var existing = await _baseplates.Find(d => d.Id == baseplate.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(baseplate);
        doc.CreatedAt = existing.CreatedAt;
        doc.ImageCached = existing.ImageCached;
        // No field is silently preserved here: the service owns preserve-on-null semantics
        // (see BaseplateService.UpdateAsync). This keeps the repository a plain store and lets
        // UnquarantineAsync explicitly clear LinkedSetId.
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        await _baseplates.ReplaceOneAsync(d => d.Id == baseplate.Id, doc);
        return ToModel(doc);
    }

    public async Task<Baseplate?> AddOrIncrementReservationAsync(Guid baseplateId, Guid setId, int quantity)
    {
        // find + guarded replace with optimistic Version retry (allowed alternative to $push/$inc).
        for (var attempt = 0; attempt < MaxReservationRetries; attempt++)
        {
            var doc = await _baseplates.Find(d => d.Id == baseplateId).FirstOrDefaultAsync();
            if (doc is null) return null;

            // Reservations are allowed to exceed the owned quantity: an over-reservation is a
            // warning surfaced to the UI ("fix the quantity"), never a blocking error. The user
            // reconciles the owned count from the baseplate library.

            var existingReservation = doc.Reservations.FirstOrDefault(r => r.SetId == setId);
            if (existingReservation is not null)
                existingReservation.Quantity += quantity;
            else
                doc.Reservations.Add(new BaseplateReservationDocument
                {
                    SetId = setId,
                    Quantity = quantity,
                    CreatedAt = DateTime.UtcNow,
                });

            doc.UpdatedAt = DateTime.UtcNow;
            var expectedVersion = doc.Version;
            doc.Version = expectedVersion + 1;

            var result = await _baseplates.ReplaceOneAsync(
                d => d.Id == baseplateId && d.Version == expectedVersion, doc);

            if (result.ModifiedCount > 0) return ToModel(doc);
        }

        throw new ConcurrencyException(
            $"Baseplate {baseplateId} was modified repeatedly while adding a reservation. Please retry.");
    }

    public async Task<bool> RemoveReservationAsync(Guid baseplateId, Guid setId)
    {
        var update = Builders<BaseplateDocument>.Update
            .PullFilter(d => d.Reservations, r => r.SetId == setId)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        var result = await _baseplates.UpdateOneAsync(d => d.Id == baseplateId, update);
        return result.ModifiedCount > 0;
    }

    public async Task RemoveReservationsBySetIdAsync(Guid setId)
    {
        var filter = Builders<BaseplateDocument>.Filter
            .ElemMatch(d => d.Reservations, r => r.SetId == setId);
        var update = Builders<BaseplateDocument>.Update
            .PullFilter(d => d.Reservations, r => r.SetId == setId)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _baseplates.UpdateManyAsync(filter, update);
    }

    private static Baseplate ToModel(BaseplateDocument doc) => new()
    {
        Id = doc.Id,
        Type = doc.Type,
        PartNum = doc.PartNum,
        Name = doc.Name,
        WidthStuds = doc.WidthStuds,
        DepthStuds = doc.DepthStuds,
        LegoColorId = doc.LegoColorId,
        ImageCached = doc.ImageCached,
        LinkedSetId = doc.LinkedSetId,
        RoadShape = doc.RoadShape,
        Quantity = doc.Quantity,
        Reservations = doc.Reservations.Select(r => new BaseplateReservation
        {
            SetId = r.SetId,
            Quantity = r.Quantity,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)),
        }).ToList(),
        Notes = doc.Notes,
        NeedsReview = doc.NeedsReview,
        Quarantined = doc.Quarantined,
        QuarantineReason = doc.QuarantineReason,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static BaseplateDocument ToDocument(Baseplate model) => new()
    {
        Id = model.Id,
        Type = model.Type,
        PartNum = model.PartNum,
        Name = model.Name,
        WidthStuds = model.WidthStuds,
        DepthStuds = model.DepthStuds,
        LegoColorId = model.LegoColorId,
        ImageCached = model.ImageCached,
        LinkedSetId = model.LinkedSetId,
        RoadShape = model.RoadShape,
        Quantity = model.Quantity,
        Reservations = model.Reservations.Select(r => new BaseplateReservationDocument
        {
            SetId = r.SetId,
            Quantity = r.Quantity,
            CreatedAt = r.CreatedAt.UtcDateTime,
        }).ToList(),
        Notes = model.Notes,
        NeedsReview = model.NeedsReview,
        Quarantined = model.Quarantined,
        QuarantineReason = model.QuarantineReason,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
