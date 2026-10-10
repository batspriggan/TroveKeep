using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class TableTemplateRepository : ITableTemplateRepository
{
    private readonly IMongoCollection<TableTemplateDocument> _templates;
    private readonly ICollectionScope _scope;

    public TableTemplateRepository(IMongoDatabase database, ICollectionScope? scope = null)
    {
        _templates = database.GetCollection<TableTemplateDocument>("table_templates");
        _scope = scope ?? UnscopedCollectionScope.Instance;
    }

    public async Task<IEnumerable<TableTemplate>> GetAllAsync()
    {
        var docs = await _templates
            .Find(_scope.ScopedTo<TableTemplateDocument>(d => d.CollectionId))
            .ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<TableTemplate?> GetByIdAsync(Guid id)
    {
        var filter = _scope.ScopedToAccessible<TableTemplateDocument>(d => d.CollectionId,
            Builders<TableTemplateDocument>.Filter.Eq(x => x.Id, id));
        var doc = await _templates.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<TableTemplate> CreateAsync(TableTemplate template)
    {
        template.CollectionId = _scope.ResolveCreateCollectionId(template.CollectionId);
        var doc = ToDocument(template);
        doc.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        doc.Version = 0;
        await _templates.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<TableTemplate?> UpdateAsync(TableTemplate template)
    {
        var scopeFilter = _scope.ScopedTo<TableTemplateDocument>(d => d.CollectionId,
            Builders<TableTemplateDocument>.Filter.Eq(x => x.Id, template.Id));
        var existing = await _templates.Find(scopeFilter).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(template);
        // A write never moves a row across the isolation boundary.
        doc.CollectionId = existing.CollectionId;
        doc.CreatedAt = existing.CreatedAt;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.Version = existing.Version + 1;

        var result = await _templates.ReplaceOneAsync(
            x => x.Id == template.Id && x.Version == template.Version, doc);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Template {template.Id} was modified by someone else. Please refresh and try again.");

        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filter = _scope.ScopedTo<TableTemplateDocument>(d => d.CollectionId,
            Builders<TableTemplateDocument>.Filter.Eq(x => x.Id, id));
        var result = await _templates.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    private static TableTemplate ToModel(TableTemplateDocument doc) => new()
    {
        Id = doc.Id,
        CollectionId = doc.CollectionId,
        Description = doc.Description,
        WidthCm = doc.WidthCm,
        DepthCm = doc.DepthCm,
        Color = doc.Color,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
        Version = doc.Version,
    };

    private static TableTemplateDocument ToDocument(TableTemplate model) => new()
    {
        Id = model.Id,
        CollectionId = model.CollectionId,
        Description = model.Description,
        WidthCm = model.WidthCm,
        DepthCm = model.DepthCm,
        Color = model.Color,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
