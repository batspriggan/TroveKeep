namespace TroveKeep.Core.Models;

public class SetImage
{
    public string SetNum { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "";
    public DateTimeOffset DownloadedAt { get; set; }
}
