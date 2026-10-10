using Microsoft.AspNetCore.Identity;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly ICollectionMemberRepository _members;
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>Minimum password length enforced on user creation and password reset.</summary>
    public const int MinPasswordLength = 8;

    public UserService(IUserRepository users, ICollectionMemberRepository members)
    {
        _users = users;
        _members = members;
    }

    public Task<IEnumerable<User>> GetAllAsync() => _users.GetAllAsync();

    public Task<User?> GetByIdAsync(Guid id) => _users.GetByIdAsync(id);

    public async Task<User> CreateAsync(string email, string displayName, string password, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new InvalidOperationException($"Password must be at least {MinPasswordLength} characters long.");

        var normalized = User.NormalizeEmail(email);
        if (await _users.GetByEmailAsync(normalized) is not null)
            throw new InvalidOperationException($"A user with email '{normalized}' already exists.");

        var user = new User
        {
            Email = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim(),
            Role = role,
            Active = true,
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        return await _users.CreateAsync(user);
    }

    public async Task<User?> UpdateAsync(Guid id, string displayName, UserRole role, bool active)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null) return null;

        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.Email : displayName.Trim();
        user.Role = role;
        user.Active = active;
        return await _users.UpdateAsync(user);
    }

    public async Task<bool> SetPasswordAsync(Guid id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            throw new InvalidOperationException($"Password must be at least {MinPasswordLength} characters long.");

        var user = await _users.GetByIdAsync(id);
        if (user is null) return false;

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _users.UpdateAsync(user);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Memberships are meaningless without the user: drop them first.
        await _members.RemoveAllByUserAsync(id);
        return await _users.DeleteAsync(id);
    }
}
