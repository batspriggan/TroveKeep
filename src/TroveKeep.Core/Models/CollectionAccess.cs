namespace TroveKeep.Core.Models;

/// <summary>
/// What a user may do inside a single collection. Stored on the membership
/// (<see cref="CollectionMember"/>), because the same user can be a different kind of member
/// in different collections.
/// </summary>
public enum CollectionAccess
{
    /// <summary>Read-only access to the collection.</summary>
    Viewer = 0,

    /// <summary>Read and write access to the collection's data.</summary>
    Editor = 1,

    /// <summary>Full control, including managing the collection's members.</summary>
    Owner = 2,
}
