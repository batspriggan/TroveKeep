using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IScannerService
{
    /// <summary>
    /// Resolves a scanned piece code (already parsed to business key) to the piece and
    /// its storage allocations, or null when no piece matches.
    /// </summary>
    Task<ScannerResult?> ResolvePieceAsync(string legoId, int legoColorId);
}
