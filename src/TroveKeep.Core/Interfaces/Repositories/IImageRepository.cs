using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IImageRepository
{
    Task<Image?> GetAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null);
    Task StoreAsync(Image image);
    Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null);
}
