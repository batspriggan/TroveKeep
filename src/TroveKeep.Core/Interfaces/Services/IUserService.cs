using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IUserService
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);

    /// <summary>Creates a local account with a password. Email is normalised and must be unique.</summary>
    Task<User> CreateAsync(string email, string displayName, string password, UserRole role);

    Task<User?> UpdateAsync(Guid id, string displayName, UserRole role, bool active);

    Task<bool> SetPasswordAsync(Guid id, string newPassword);

    Task<bool> DeleteAsync(Guid id);
}
