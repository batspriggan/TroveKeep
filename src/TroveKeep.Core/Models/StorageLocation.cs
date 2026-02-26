namespace TroveKeep.Core.Models;

public enum StorageType
{
    Box,
    Drawer
}

public class StorageLocation
{
    public StorageType Type { get; set; }
    public Guid StorageId { get; set; }
    public string StorageName { get; set; } = string.Empty;
    public Guid? DrawerContainerId { get; set; }
    public string? DrawerContainerName { get; set; }
    public int? DrawerPosition { get; set; }
}
