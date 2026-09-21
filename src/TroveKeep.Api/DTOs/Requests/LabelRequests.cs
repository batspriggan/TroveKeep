namespace TroveKeep.Api.DTOs.Requests;

/// <summary>Request body for <c>POST /api/labels/print</c>.</summary>
/// <param name="Kind">Target kind: <c>set</c>, <c>bulkpiece</c>, <c>box-summary</c>, <c>box-qr</c>, <c>box-pieces</c>, <c>container-pieces</c>.</param>
/// <param name="Id">Id of the target entity.</param>
/// <param name="Copies">Optional copies override.</param>
/// <param name="Size">Optional size override (set labels only).</param>
public sealed record PrintLabelRequest(string Kind, Guid Id, int? Copies = null, string? Size = null);

/// <summary>Request body for <c>PUT /api/labels/config</c>.</summary>
public sealed record LabelConfigRequest(string Mode, string? ServerUrl = null, string? ServerToken = null);
