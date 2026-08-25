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

    /// <summary>
    /// Builds a bulk-piece label addressed to a specific storage location:
    /// line 1 = "{legoId} {colorName}", line 2 = location, then QR + image.
    /// </summary>
    string BuildBulkPieceLocationLabel(BulkPiece piece, string? colorName, string? locationLine, int? copies = null);
    /// <summary>Unique file name for a location-addressed piece label.</summary>
    string GetBulkPieceLocationFileName(BulkPiece piece, int index);

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
