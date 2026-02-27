using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface ISetImageRepository
{
    Task<SetImage?> GetAsync(string setNum);
    Task StoreAsync(SetImage image);
}
