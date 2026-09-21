using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

/// <summary>
/// Builds the JSON documents for labels in the label-tool file format.
/// <para>
/// Without an <see cref="ILabelImageResolver"/> the label references images by URL;
/// with one (server mode) the images are embedded inline as base64.
/// </para>
/// </summary>
public interface ILabelPrintService
{
    // ---- Bulk piece ----
    Task<string> BuildBulkPieceLabel(BulkPiece piece, ILabelImageResolver? images = null, int? copies = null, string? size = null);
    string GetBulkPieceFileName(BulkPiece piece);

    /// <summary>
    /// Builds a bulk-piece label addressed to a specific storage location:
    /// line 1 = "{legoId} {colorName}", line 2 = location, then QR + image.
    /// </summary>
    Task<string> BuildBulkPieceLocationLabel(BulkPiece piece, string? colorName, string? locationLine, ILabelImageResolver? images = null, int? copies = null, string? qrValue = null);
    /// <summary>Unique file name for a location-addressed piece label.</summary>
    string GetBulkPieceLocationFileName(BulkPiece piece, int index);

    // ---- Set ----
    Task<string> BuildLegoSetLabel(LegoSet set, ILabelImageResolver? images = null, int? copies = null, string? size = null);
    string GetLegoSetFileName(LegoSet set);

    // ---- Box (summary = "large" with content overview) ----
    string BuildBoxSummaryLabel(Box box, int? copies = null);
    string GetBoxSummaryFileName(Box box);

    // ---- Box (qr = "small" with the box code) ----
    string BuildBoxQrLabel(Box box, int? copies = null);
    string GetBoxQrFileName(Box box);
}
