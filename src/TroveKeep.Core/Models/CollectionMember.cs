namespace TroveKeep.Core.Models;

/// <summary>
/// Join row between a <see cref="User"/> and a <see cref="Collection"/>. Stored in its own
/// collection (<c>collection_members</c>) rather than embedded, so memberships can be listed
/// and updated independently of either side.
/// </summary>
public class CollectionMember
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Guid UserId { get; set; }
    public CollectionAccess Access { get; set; } = CollectionAccess.Editor;
    public DateTimeOffset AddedAt { get; set; }
}
