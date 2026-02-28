using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class ImageService : IImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IImageRepository _repository;
	private readonly ILegoSetService _legoSetService;
	private readonly IBulkPieceService _bulkPieceService;

	public ImageService(IHttpClientFactory httpClientFactory,
        IImageRepository repository,
        ILegoSetService legoSetService,
        IBulkPieceService bulkPieceService)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
		_legoSetService = legoSetService;
		_bulkPieceService = bulkPieceService;
	}

    public async Task<bool> DownloadAndStoreAsync(Guid Id, string referenceNumber, string imgUrl, ImageReferenceType referenceType)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SetImages");
            using var response = await client.GetAsync(imgUrl);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var data = await response.Content.ReadAsByteArrayAsync();

            await _repository.StoreAsync(new Image
            {
                ReferenceNumber = referenceNumber,
                Data = data,
                ContentType = contentType,
                DownloadedAt = DateTimeOffset.UtcNow,
                ReferenceType = referenceType,
            });
            if(referenceType == ImageReferenceType.Set)
                await _legoSetService.UpdateImageCachedAsync(Id).ConfigureAwait(false);
            else if(referenceType == ImageReferenceType.Part)
                await _bulkPieceService.UpdateImageCachedAsync(Id).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<Image?> GetImageAsync(string referenceNumber, ImageReferenceType referenceType) =>
        _repository.GetAsync(referenceNumber, referenceType);
}
