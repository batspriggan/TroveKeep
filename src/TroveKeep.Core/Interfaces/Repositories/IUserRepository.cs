using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IUserRepository
{
    /// <summary>Case-insensitive lookup by email (stored lower-cased).</summary>
    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByIdAsync(Guid id);

    Task<IEnumerable<User>> GetAllAsync();

    Task<User> CreateAsync(User user);

    Task<User?> UpdateAsync(User user);

    Task<bool> DeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);
}
