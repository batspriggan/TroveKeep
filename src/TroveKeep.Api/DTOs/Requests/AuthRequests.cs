namespace TroveKeep.Api.DTOs.Requests;

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CreateUserRequest(string Email, string DisplayName, string Password, string Role = "User");

public record UpdateUserRequest(string DisplayName, string Role, bool Active);

public record SetUserPasswordRequest(string NewPassword);

public record CreateCollectionRequest(string Name, string? Description = null);

public record UpdateCollectionRequest(string Name, string? Description = null);

public record SetCollectionMemberRequest(Guid UserId, string Access = "Editor");
