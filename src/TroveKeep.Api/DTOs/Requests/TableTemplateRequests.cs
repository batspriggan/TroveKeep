namespace TroveKeep.Api.DTOs.Requests;

public record CreateTableTemplateRequest(string Description, int WidthCm, int DepthCm, string Color);

public record UpdateTableTemplateRequest(string Description, int WidthCm, int DepthCm, string Color, int Version = 0);
