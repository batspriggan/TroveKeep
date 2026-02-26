using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BulkPieceService : IBulkPieceService
{
    public Task<IEnumerable<BulkPiece>> GetAllAsync() => throw new NotImplementedException();
    public Task<BulkPiece?> GetByIdAsync(Guid id) => throw new NotImplementedException();
    public Task<BulkPiece> CreateAsync(BulkPiece bulkPiece) => throw new NotImplementedException();
    public Task<BulkPiece?> UpdateAsync(BulkPiece bulkPiece) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<StorageLocation?> GetStorageAsync(Guid id) => throw new NotImplementedException();
    public Task<BulkPiece?> AssignToBoxAsync(Guid id, Guid boxId) => throw new NotImplementedException();
    public Task<BulkPiece?> AssignToDrawerAsync(Guid id, Guid drawerId) => throw new NotImplementedException();
    public Task<bool> RemoveStorageAsync(Guid id) => throw new NotImplementedException();
}
