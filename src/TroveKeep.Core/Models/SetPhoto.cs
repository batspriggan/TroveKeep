namespace TroveKeep.Core.Models;

public class SetPhoto
{
    public Guid Id { get; set; }
    public Guid SetId { get; set; }
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}
