using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class SetImageService : ISetImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISetImageRepository _repository;
    private readonly ILegoSetRepository _legoSetRepository;

    public SetImageService(IHttpClientFactory httpClientFactory, ISetImageRepository repository, ILegoSetRepository legoSetRepository)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _legoSetRepository = legoSetRepository;
    }

    public async Task<bool> DownloadAndStoreAsync(Guid setId, string setNum, string imgUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SetImages");
            using var response = await client.GetAsync(imgUrl);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var data = await response.Content.ReadAsByteArrayAsync();

            await _repository.StoreAsync(new SetImage
            {
                SetNum = setNum,
                Data = data,
                ContentType = contentType,
                DownloadedAt = DateTimeOffset.UtcNow,
            });
            await _legoSetRepository.UpdateImageCachedAsync(setId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<SetImage?> GetImageAsync(string setNum) =>
        _repository.GetAsync(setNum);
}
