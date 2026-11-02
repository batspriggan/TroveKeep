namespace TroveKeep.Api.DTOs.Responses;

public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool Active,
    bool HasPassword,
    string? ExternalProvider,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// The caller's own identity plus the collections they can act on. This is what the SPA uses to
/// drive navigation and role gating.
/// </summary>
public record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    IReadOnlyList<CollectionSummaryResponse> Collections);

public record CollectionSummaryResponse(Guid Id, string Name, string Access);

/// <summary>Which login methods the SPA should offer. Local auth is always available.</summary>
public record ProvidersResponse(bool Local, bool Oidc);

public record CollectionResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CollectionMemberResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Access,
    DateTimeOffset AddedAt);

/// <summary>
/// A user that could be added to a collection, for the member picker. Deliberately reduced to the
/// identity fields: the collection Owner needs to recognise the account, not to administer it
/// (the full user list stays admin-only — <c>UsersController</c>).
/// </summary>
public record CollectionMemberCandidateResponse(
    Guid UserId,
    string Email,
    string DisplayName);
