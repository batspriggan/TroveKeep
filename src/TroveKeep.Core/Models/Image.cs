namespace TroveKeep.Core.Models;

public class Image
{
    required public string ReferenceNumber { get; set; }
    required public byte[] Data { get; set; }
    required public string ContentType { get; set; }
    required public DateTimeOffset DownloadedAt { get; set; }
    required public ImageReferenceType ReferenceType { get; set; }
}

public enum ImageReferenceType
{
    Set,
    Part,
    Box,
    DrawerContainer,
    Baseplate
}
