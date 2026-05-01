namespace TroveKeep.Core.Models;

public class AggregateBpLayout
{
    public string RepresentativeId { get; set; } = string.Empty;
    public List<PlacedBaseplate> PlacedBaseplates { get; set; } = [];
}
