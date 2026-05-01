using MongoDB.Driver;
using TroveKeep.Core.Exceptions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class TableTemplateRepository : ITableTemplateRepository
{
    private readonly IMongoCollection<TableTemplateDocument> _templates;

    public TableTemplateRepository(IMongoDatabase database)
    {
        _templates = database.GetCollection<TableTemplateDocument>("table_templates");
    }

    public async Task<IEnumerable<TableTemplate>> GetAllAsync()
    {
        var docs = await _templates.Find(_ => true).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<TableTemplate?> GetByIdAsync(Guid id)
    {
        var doc = await _templates.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<TableTemplate> CreateAsync(TableTemplate template)
    {
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
        var existing = await _templates.Find(x => x.Id == template.Id).FirstOrDefaultAsync();
        if (existing is null) return null;

        var doc = ToDocument(template);
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
        var result = await _templates.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    private static TableTemplate ToModel(TableTemplateDocument doc) => new()
    {
        Id = doc.Id,
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
        Description = model.Description,
        WidthCm = model.WidthCm,
        DepthCm = model.DepthCm,
        Color = model.Color,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
        Version = model.Version,
    };
}
