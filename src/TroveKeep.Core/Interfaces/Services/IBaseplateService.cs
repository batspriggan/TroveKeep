using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IBaseplateService
{
    Task<IEnumerable<Baseplate>> GetAllAsync();
    Task<Baseplate?> GetByIdAsync(Guid id);
    Task<Baseplate> CreateAsync(Baseplate baseplate);
    Task<Baseplate> CreateAsync(Baseplate baseplate, bool isImported);
    Task UpdateImageCachedAsync(Guid id, bool cached);
    Task DeleteAsync(Guid id);
    (int studX, int studY) GuessStudDimensions(string partDescription);
    Task<Baseplate> UpdateAsync(Guid id, Baseplate updated);
    Task<Baseplate> AddReservationAsync(Guid id, Guid setId, int quantity);
    Task<Baseplate> RemoveReservationAsync(Guid id, Guid setId);
    Task RemoveReservationsBySetIdAsync(Guid setId);
    Task<Baseplate> ConfirmAsync(Guid id);

    /// <summary>
    /// Clears the quarantine flag. When <paramref name="confirmAsPhysicalPlate"/> is true the row
    /// is treated as a real physical plate: <c>LinkedSetId</c> is cleared (Type stays Custom).
    /// </summary>
    Task<Baseplate> UnquarantineAsync(Guid id, bool confirmAsPhysicalPlate);

    bool ComputeNeedsReview(Baseplate bp, bool isImported);
}
