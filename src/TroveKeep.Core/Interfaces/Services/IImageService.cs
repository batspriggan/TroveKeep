using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IImageService
{
    Task<bool> DownloadAndStoreAsync(Guid Id, string referenceNumber, string imgUrl, ImageReferenceType referenceType, int? colorId = null);
    Task<Image?> GetImageAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null);
    Task StoreUploadAsync(string referenceNumber, ImageReferenceType referenceType, Stream stream, string contentType, int? colorId = null);
    Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null);
}
