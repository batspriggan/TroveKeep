namespace TroveKeep.Api.DTOs.Requests;

public record CreateBulkPieceRequest(string LegoId, string LegoColor, string Description, int Quantity = 1);

public record UpdateBulkPieceRequest(string LegoId, string LegoColor, string Description, int Quantity = 1);
