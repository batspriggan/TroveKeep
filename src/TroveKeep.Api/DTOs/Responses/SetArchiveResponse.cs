namespace TroveKeep.Api.DTOs.Responses;

public record SetArchiveResponse(string SetNum, string Name, int Year, int ThemeId, int NumParts, string ImgUrl);
