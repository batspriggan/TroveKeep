namespace TroveKeep.Api.DTOs.Responses;

public record PagedResponse<T>(
    IEnumerable<T> Items,
    long Total,
    int Page,
    int Size,
    int TotalPages);
