using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class SetPhotoService : ISetPhotoService
{
    private readonly ISetPhotoRepository _repo;

    public SetPhotoService(ISetPhotoRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<SetPhoto>> GetBySetIdAsync(Guid setId) =>
        _repo.GetMetadataBySetIdAsync(setId);

    public Task<SetPhoto?> GetByIdAsync(Guid id) =>
        _repo.GetByIdAsync(id);

    public Task<Dictionary<Guid, int>> GetCountsBySetIdsAsync(IEnumerable<Guid> setIds) =>
        _repo.GetCountsBySetIdsAsync(setIds);

    public async Task<Guid> UploadAsync(Guid setId, Stream stream, string contentType)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return await _repo.StoreAsync(setId, ms.ToArray(), contentType);
    }

    public Task<bool> DeleteAsync(Guid id) => _repo.DeleteAsync(id);

    public Task DeleteBySetIdAsync(Guid setId) => _repo.DeleteBySetIdAsync(setId);
}
