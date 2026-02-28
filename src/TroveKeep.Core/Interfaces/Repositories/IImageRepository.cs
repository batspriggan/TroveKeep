using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IImageRepository
{
    Task<Image?> GetAsync(string setNum, ImageReferenceType referenceType);
    Task StoreAsync(Image image);
}
