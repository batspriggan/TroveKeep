using Microsoft.AspNetCore.Identity;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;

namespace TroveKeep.Api.Auth;

/// <summary>
/// First-run provisioning. Runs after migrations: migration 006 has already created the default
/// collection and flagged <c>meta.bootstrap_pending</c>, but it cannot create the admin itself
/// (password hashing belongs to the application layer).
///
/// <para>
/// On the very first start of a fresh database the admin credentials must be supplied via
/// <c>Auth:BootstrapAdminEmail</c> / <c>Auth:BootstrapAdminPassword</c>. Refusing to start
/// without them is deliberate: an unauthenticated instance that silently creates an admin with a
/// guessed password would be worse than a loud failure.
/// </para>
/// </summary>
public class BootstrapService
{
    private readonly IUserRepository _users;
    private readonly ICollectionRepository _collections;
    private readonly ICollectionMemberRepository _members;
    private readonly IConfiguration _config;
    private readonly ILogger<BootstrapService> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    public BootstrapService(
        IUserRepository users,
        ICollectionRepository collections,
        ICollectionMemberRepository members,
        IConfiguration config,
        ILogger<BootstrapService> logger)
    {
        _users = users;
        _collections = collections;
        _members = members;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var existing = (await _users.GetAllAsync()).ToList();
        if (existing.Count > 0)
        {
            _logger.LogInformation("Users already exist ({Count}); skipping bootstrap.", existing.Count);
            return;
        }

        var email = _config["Auth:BootstrapAdminEmail"];
        var password = _config["Auth:BootstrapAdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No users exist yet and Auth:BootstrapAdminEmail / Auth:BootstrapAdminPassword are not set. " +
                "Set them (env: Auth__BootstrapAdminEmail / Auth__BootstrapAdminPassword) to create the first admin.");
        }

        // The default collection is created by migration 006. If the migration did not run (fresh
        // DB where migrations are disabled), create it here so the admin always has somewhere to go.
        var collection = (await _collections.GetAllAsync()).FirstOrDefault();
        if (collection is null)
        {
            collection = await _collections.CreateAsync(new Collection
            {
                Name = _config["Migration:DefaultCollectionName"] ?? "Casa",
                Description = "Default collection created at bootstrap.",
            });
            _logger.LogInformation("Created default collection '{Name}'.", collection.Name);
        }

        var admin = new User
        {
            Email = User.NormalizeEmail(email),
            DisplayName = User.NormalizeEmail(email),
            Role = UserRole.Admin,
            Active = true,
        };
        admin.PasswordHash = _hasher.HashPassword(admin, password);
        admin = await _users.CreateAsync(admin);

        await _members.AddOrUpdateAsync(new CollectionMember
        {
            CollectionId = collection.Id,
            UserId = admin.Id,
            Access = CollectionAccess.Owner,
        });

        _logger.LogInformation(
            "Bootstrap complete: admin '{Email}' created and linked as Owner of collection '{Collection}'.",
            admin.Email, collection.Name);
    }
}
