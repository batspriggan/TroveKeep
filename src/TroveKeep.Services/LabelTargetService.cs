using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class LabelTargetService : ILabelTargetService
{
    private readonly ILabelTargetRepository _repo;

    public LabelTargetService(ILabelTargetRepository repo) => _repo = repo;

    public async Task<string> GetOrCreateStorageKeyAsync(StorageType type, Guid storageId, int? position = null)
    {
        var existing = await _repo.GetByStorageAsync(storageId, position);
        var target = existing.FirstOrDefault();
        if (target is not null) return target.Key;

        var key = LabelCodes.ForStorageKey();
        var now = DateTimeOffset.UtcNow;
        await _repo.UpsertAsync(new LabelTarget
        {
            Key = key,
            TargetType = type,
            StorageId = storageId,
            StoragePosition = position,
            CreatedAt = now,
            UpdatedAt = now,
        });
        return key;
    }
}
