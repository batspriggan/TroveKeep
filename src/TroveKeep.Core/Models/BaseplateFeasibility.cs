namespace TroveKeep.Core.Models;

public class FeasibilityAggregateRef
{
    public Guid RoomId { get; set; }
    public string RepresentativeId { get; set; } = string.Empty;
}

public class BaseplateFeasibilityLine
{
    public Guid BaseplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Need { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public int Deficit { get; set; }
    public string Status { get; set; } = "ok";
}

public class BaseplateFeasibilityResult
{
    public List<BaseplateFeasibilityLine> Lines { get; set; } = [];
    public int TotalDeficit { get; set; }
    public bool HasShortage { get; set; }
}
