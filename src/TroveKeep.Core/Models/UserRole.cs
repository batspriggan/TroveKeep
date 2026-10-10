namespace TroveKeep.Core.Models;

/// <summary>
/// Global role of a user. Deliberately small:
/// <list type="bullet">
///   <item><see cref="Admin"/> — full access to everything plus user/collection management;</item>
///   <item><see cref="User"/> — full access to the collections they are a member of;</item>
///   <item><see cref="Guest"/> — read-only access to the Table Planner only.</item>
/// </list>
/// The distinction is global (not per collection): per-collection access is expressed by
/// <see cref="CollectionAccess"/> on the membership.
/// </summary>
public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
}
