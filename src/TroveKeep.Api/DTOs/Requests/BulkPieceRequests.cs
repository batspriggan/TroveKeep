namespace TroveKeep.Api.DTOs.Requests;

public record CreateBulkPieceRequest(string LegoId, int LegoColorId, string Description, int Quantity = 1);

public record UpdateBulkPieceRequest(string LegoId, int LegoColorId, string Description, int Quantity = 1, int Version = 0);
