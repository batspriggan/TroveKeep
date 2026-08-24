namespace TroveKeep.Core.Models;

public enum LabelRefKind
{
    Piece,
    Set,
    Box,
}

/// <summary>A parsed label code payload, identifying the entity it points to.</summary>
public sealed record LabelRef(
    LabelRefKind Kind,
    string? LegoId = null,
    int? ColorId = null,
    string? SetNumber = null,
    Guid? BoxId = null);

/// <summary>
/// Payload formats for the QR codes printed on labels:
/// <list type="bullet">
///   <item>Piece: <c>TK:BP:{LegoId}:{LegoColorId}</c></item>
///   <item>Set:   <c>TK:SET:{SetNumber}</c></item>
///   <item>Box:   <c>TK:BOX:{Guid:N}</c></item>
/// </list>
/// </summary>
public static class LabelCodes
{
    private const string PiecePrefix = "TK:BP:";
    private const string SetPrefix = "TK:SET:";
    private const string BoxPrefix = "TK:BOX:";

    public static string ForPiece(string legoId, int legoColorId) => $"{PiecePrefix}{legoId}:{legoColorId}";
    public static string ForSet(string setNumber) => $"{SetPrefix}{setNumber}";
    public static string ForBox(Guid boxId) => $"{BoxPrefix}{boxId:N}";

    /// <summary>Parses a scanned code into a <see cref="LabelRef"/>. Returns null for unsupported codes.</summary>
    public static LabelRef? TryParse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        if (code.StartsWith(PiecePrefix, StringComparison.Ordinal))
        {
            var payload = code[PiecePrefix.Length..];
            var sep = payload.LastIndexOf(':');
            if (sep <= 0 || sep == payload.Length - 1) return null;
            return int.TryParse(payload[(sep + 1)..], out var colorId)
                ? new LabelRef(LabelRefKind.Piece, LegoId: payload[..sep], ColorId: colorId)
                : null;
        }

        if (code.StartsWith(SetPrefix, StringComparison.Ordinal))
        {
            var setNumber = code[SetPrefix.Length..];
            return string.IsNullOrWhiteSpace(setNumber)
                ? null
                : new LabelRef(LabelRefKind.Set, SetNumber: setNumber);
        }

        if (code.StartsWith(BoxPrefix, StringComparison.Ordinal))
        {
            var value = code[BoxPrefix.Length..];
            return value.Length == 32 && Guid.TryParseExact(value, "N", out var boxId)
                ? new LabelRef(LabelRefKind.Box, BoxId: boxId)
                : null;
        }

        return null;
    }
}
