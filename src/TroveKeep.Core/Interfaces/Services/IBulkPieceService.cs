using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IBulkPieceService
{
    Task<IEnumerable<BulkPiece>> GetAllAsync();
    Task<BulkPiece?> GetByIdAsync(Guid id);
    Task<BulkPiece> CreateAsync(BulkPiece bulkPiece);
    Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece);
    Task<bool> DeleteAsync(Guid id);
    Task<BulkPiece?> AllocateToBoxAsync(Guid id, Guid boxId, int quantity);
    Task<BulkPiece?> AllocateToDrawerAsync(Guid id, Guid containerId, int position, int quantity);
    Task<BulkPiece?> DeallocateFromBoxAsync(Guid id, Guid boxId);
    Task<BulkPiece?> DeallocateFromDrawerAsync(Guid id, Guid containerId, int position);
    Task<BulkPiece?> ClearStorageAsync(Guid id);
    Task UpdateImageCachedAsync(Guid id);
}
