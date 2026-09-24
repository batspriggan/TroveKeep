namespace TroveKeep.Core.Models;

public class BaseplateReservation
{
    public Guid SetId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
