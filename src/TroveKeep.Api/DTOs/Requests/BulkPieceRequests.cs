namespace TroveKeep.Api.DTOs.Requests;

public record CreateBulkPieceRequest(string LegoId, string LegoColor, string Description);

public record UpdateBulkPieceRequest(string LegoId, string LegoColor, string Description);
