using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IBulkPieceService
{
    Task<IEnumerable<BulkPiece>> GetAllAsync();
    Task<BulkPiece?> GetByIdAsync(Guid id);
    Task<BulkPiece> CreateAsync(BulkPiece bulkPiece);
    Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece);
    Task<bool> DeleteAsync(Guid id);
    Task<StorageLocation?> GetStorageAsync(Guid id);
    Task<BulkPiece?> AssignToBoxAsync(Guid id, Guid boxId);
    Task<BulkPiece?> AssignToDrawerAsync(Guid id, Guid drawerId);
    Task<bool> RemoveStorageAsync(Guid id);
}
