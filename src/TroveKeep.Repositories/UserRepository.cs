using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Repositories.Documents;

namespace TroveKeep.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<UserDocument>("users");
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        var doc = await _users.Find(x => x.Email == normalized).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var doc = await _users.Find(x => x.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<User?> GetSystemUserAsync()
    {
        var doc = await _users.Find(x => x.IsSystem).FirstOrDefaultAsync();
        return doc is null ? null : ToModel(doc);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        var docs = await _users.Find(_ => true).SortBy(x => x.Email).ToListAsync();
        return docs.Select(ToModel);
    }

    public async Task<User> CreateAsync(User user)
    {
        var doc = ToDocument(user);
        doc.Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id;
        doc.Email = NormalizeEmail(doc.Email);
        var now = DateTime.UtcNow;
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        await _users.InsertOneAsync(doc);
        return ToModel(doc);
    }

    public async Task<User?> UpdateAsync(User user)
    {
        var doc = ToDocument(user);
        doc.Email = NormalizeEmail(doc.Email);
        doc.UpdatedAt = DateTime.UtcNow;

        var existing = await _users.Find(x => x.Id == user.Id).FirstOrDefaultAsync();
        if (existing is null) return null;
        doc.CreatedAt = existing.CreatedAt;

        await _users.ReplaceOneAsync(x => x.Id == user.Id, doc);
        return ToModel(doc);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _users.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _users.Find(x => x.Id == id).AnyAsync();

    /// <summary>Emails are stored lower-cased and trimmed so lookups are case-insensitive.</summary>
    public static string NormalizeEmail(string email) => User.NormalizeEmail(email);

    private static User ToModel(UserDocument doc) => new()
    {
        Id = doc.Id,
        Email = doc.Email,
        DisplayName = doc.DisplayName,
        PasswordHash = doc.PasswordHash,
        Role = (UserRole)doc.Role,
        Active = doc.Active,
        IsSystem = doc.IsSystem,
        ExternalProvider = doc.ExternalProvider,
        ExternalSubject = doc.ExternalSubject,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)),
    };

    private static UserDocument ToDocument(User model) => new()
    {
        Id = model.Id,
        Email = model.Email,
        DisplayName = model.DisplayName,
        PasswordHash = model.PasswordHash,
        Role = (int)model.Role,
        Active = model.Active,
        IsSystem = model.IsSystem,
        ExternalProvider = model.ExternalProvider,
        ExternalSubject = model.ExternalSubject,
        CreatedAt = model.CreatedAt.UtcDateTime,
        UpdatedAt = model.UpdatedAt.UtcDateTime,
    };
}
