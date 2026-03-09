using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface ISetPhotoService
{
    Task<IEnumerable<SetPhoto>> GetBySetIdAsync(Guid setId);
    Task<SetPhoto?> GetByIdAsync(Guid id);
    Task<Dictionary<Guid, int>> GetCountsBySetIdsAsync(IEnumerable<Guid> setIds);
    Task<Guid> UploadAsync(Guid setId, Stream stream, string contentType);
    Task<bool> DeleteAsync(Guid id);
    Task DeleteBySetIdAsync(Guid setId);
}
