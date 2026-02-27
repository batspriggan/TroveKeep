using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ISetImageService
{
    Task<bool> DownloadAndStoreAsync(Guid setId, string setNum, string imgUrl);
    Task<SetImage?> GetImageAsync(string setNum);
}
