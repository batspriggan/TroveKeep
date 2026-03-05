using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IImageService
{
    Task<bool> DownloadAndStoreAsync(Guid Id, string referenceNumber, string imgUrl, ImageReferenceType referenceType);
    Task<Image?> GetImageAsync(string referenceNumber, ImageReferenceType referenceType);
    Task StoreUploadAsync(string referenceNumber, ImageReferenceType referenceType, Stream stream, string contentType);
    Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType);
}
