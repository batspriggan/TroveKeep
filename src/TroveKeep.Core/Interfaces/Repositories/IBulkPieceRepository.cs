using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Repositories;

public interface IBulkPieceRepository
{
    Task<IEnumerable<BulkPiece>> GetAllAsync();
    Task<BulkPiece?> GetByIdAsync(Guid id);
    Task<IEnumerable<BulkPiece>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<BulkPiece> CreateAsync(BulkPiece bulkPiece);
    Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<BulkPiece>> SearchAsync(string query);
    Task UpdateImageCachedAsync(Guid id);
}
