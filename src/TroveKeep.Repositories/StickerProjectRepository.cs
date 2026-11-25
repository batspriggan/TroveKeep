using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

/// <summary>
/// Repository of the global sticker projects (collection <c>sticker_projects</c>).
///
/// <para>
/// Like the image library it is not collection-scoped: a project belongs to no collection, so there
/// is no <c>ICollectionScope</c> filter. The embedded items make a project a single document, so an
/// update is one optimistic replace (version mismatch → <see cref="ConcurrencyException"/>).
/// </para>
/// </summary>
public class StickerProjectRepository : IStickerProjectRepository
{
    private readonly IMongoCollection<StickerProjectDocument> _projects;

    public StickerProjectRepository(IMongoDatabase database)
    {
        _projects = database.GetCollection<StickerProjectDocument>("sticker_projects");
    }

    public async Task<IEnumerable<StickerProject>> GetAllAsync()
    {
        var docs = await _projects.Find(_ => true).ToListAsync();
        return docs
            .Select(ToModel)
            // Deterministic order so the project list does not depend on MongoDB's natural order.
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id);
    }

    public async Task<StickerProject?> GetByIdAsync(Guid id)
    {
        var doc = await _projects.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<StickerProject> CreateAsync(StickerProject project)
    {
        var doc = ToDocument(project);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _projects.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<StickerProject?> UpdateAsync(StickerProject project)
    {
        var existing = await _projects.Find(x => x.Id == project.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(project);
        doc.Id = project.Id;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _projects.ReplaceOneAsync(
            x => x.Id == project.Id && x.Version == project.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException(
                $"Sticker project {project.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _projects.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<int> CountUsagesAsync(Guid imageId)
    {
        var docs = await _projects
            .Find(x => x.Items.Any(i => i.ImageId == imageId))
            .ToListAsync();
        return docs.Sum(d => d.Items.Count(i => i.ImageId == imageId));
    }

    private static StickerProject ToModel(StickerProjectDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Items = doc.Items.Select(i => new StickerItem
        {
            Id = i.Id,
            ImageId = i.ImageId,
            Mount = i.Mount,
            WidthStuds = i.WidthStuds,
            DepthStuds = i.DepthStuds,
            BrickHeight = i.BrickHeight,
            Copies = i.Copies,
            Fit = i.Fit,
            Orientation = i.Orientation,
            Notes = i.Notes,
        }).ToList(),
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static StickerProjectDocument ToDocument(StickerProject model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Items = model.Items.Select(i => new StickerItemDocument
        {
            Id = i.Id,
            ImageId = i.ImageId,
            Mount = i.Mount,
            WidthStuds = i.WidthStuds,
            DepthStuds = i.DepthStuds,
            BrickHeight = i.BrickHeight,
            Copies = i.Copies,
            Fit = i.Fit,
            Orientation = i.Orientation,
            Notes = i.Notes,
        }).ToList(),
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
