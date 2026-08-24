using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Builds the JSON document for a bulk-piece label in the label-tool file format.
/// The UI downloads it to a locally monitored folder; nothing is written here.
/// </summary>
public interface ILabelPrintService
{
    /// <summary>Returns the label JSON text (serialized) for the given piece, or null if it cannot be built.</summary>
    string? BuildBulkPieceLabel(BulkPiece piece, int? copies = null, string? size = null);

    /// <summary>Suggested local file name for the downloaded label JSON (unique per piece).</summary>
    string GetBulkPieceFileName(BulkPiece piece);
}
