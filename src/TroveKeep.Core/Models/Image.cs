namespace TroveKeep.Core.Models;

public class Image
{
    required public string ReferenceNumber { get; set; }
    /// <summary>Optional color id — only for part (Piece) images, where the image is per (partNum, colorId). Null for set/box/drawer/baseplate.</summary>
    public int? ColorId { get; set; }
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
