using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBulkPieceRepository
{
    Task<IEnumerable<BulkPiece>> GetAllAsync();
    Task<BulkPiece?> GetByIdAsync(Guid id);
    Task<BulkPiece> CreateAsync(BulkPiece bulkPiece);
    Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece);
    Task<bool> DeleteAsync(Guid id);
    Task<BulkPiece?> AddStorageAsync(Guid id, Guid storageId, StorageType type, int quantity);
    Task<BulkPiece?> RemoveStorageAsync(Guid id, Guid storageId);
    Task<BulkPiece?> ClearStorageAsync(Guid id);
}
