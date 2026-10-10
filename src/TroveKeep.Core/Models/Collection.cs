namespace TroveKeep.Core.Models;

/// <summary>
/// A tenant: the isolation boundary for inventory data. Every owned entity
/// (sets, bulk pieces, boxes, drawer containers, baseplates, rooms, table templates) carries a
/// <c>CollectionId</c> pointing at one of these.
///
/// <para>
/// A user sees only the collections they are a member of (see <see cref="CollectionMember"/>);
/// an admin sees all of them. Ownership is therefore a property of the collection, not of
/// individual rows.
/// </para>
/// </summary>
public class Collection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
