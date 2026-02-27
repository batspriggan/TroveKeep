namespace TroveKeep.Core.Models;

public class RebrickableSet
{
    public string SetNum { get; set; } = "";
    public string Name { get; set; } = "";
    public int Year { get; set; }
    public int ThemeId { get; set; }
    public int NumParts { get; set; }
    public string ImgUrl { get; set; } = "";
}
