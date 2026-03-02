namespace TroveKeep.Repositories.Documents;

public class DrawerDocument
{
    public int Position { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
