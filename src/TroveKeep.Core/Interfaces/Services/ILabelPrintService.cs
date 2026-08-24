using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Builds the JSON documents for labels in the label-tool file format.
/// The UI downloads them to a locally monitored folder; nothing is written here.
/// </summary>
public interface ILabelPrintService
{
    // ---- Bulk piece ----
    string BuildBulkPieceLabel(BulkPiece piece, int? copies = null, string? size = null);
    string GetBulkPieceFileName(BulkPiece piece);

    // ---- Set ----
    string BuildLegoSetLabel(LegoSet set, int? copies = null, string? size = null);
    string GetLegoSetFileName(LegoSet set);

    // ---- Box (summary = "large" with content overview) ----
    string BuildBoxSummaryLabel(Box box, int? copies = null);
    string GetBoxSummaryFileName(Box box);

    // ---- Box (qr = "small" with the box code) ----
    string BuildBoxQrLabel(Box box, int? copies = null);
    string GetBoxQrFileName(Box box);
}
