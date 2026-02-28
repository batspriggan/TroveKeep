using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IRoomExportService
{
    Task<(byte[] Data, string FileName)> ExportRoomAsync(Guid roomId);
    Task<Room> ImportRoomAsync(Stream stream);
}
