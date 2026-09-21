using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// HTTP client for the remote label-tool server. See <c>docs/server-api.md</c>.
/// </summary>
/// <remarks>
/// Delivery policy mirrors the server's at-most-once guarantee:
/// <list type="bullet">
///   <item>a <c>422</c> is definitive (bad label/image) — do not retry;</item>
///   <item>a <c>503</c>/<c>5xx</c> is transient — the client may retry, the server replied;</item>
///   <item>a timeout or transport error is <see cref="LabelDispatchOutcome.Uncertain"/> —
///         the request may or may not have been accepted, so it is never retried here.</item>
/// </list>
/// </remarks>
public class LabelServerClient : ILabelServerClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LabelPrintSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public LabelServerClient(IHttpClientFactory httpClientFactory, LabelPrintSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<LabelDispatchResult> PrintAsync(string baseUrl, string? token, string labelJson, CancellationToken ct = default)
    {
        using var client = CreateClient(baseUrl, token);
        using var content = new StringContent(labelJson, Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.PostAsync("api/v1/print", content, ct);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var parsed = TryDeserialize<PrintResponse>(body);
                return new LabelDispatchResult(LabelDispatchOutcome.Accepted, parsed?.JobId, null);
            }

            var detail = await ReadDetailAsync(response, ct);

            return response.StatusCode switch
            {
                HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest
                    => new LabelDispatchResult(LabelDispatchOutcome.Invalid, null, detail),
                HttpStatusCode.ServiceUnavailable
                    => new LabelDispatchResult(LabelDispatchOutcome.Transient, null, detail),
                >= HttpStatusCode.InternalServerError
                    => new LabelDispatchResult(LabelDispatchOutcome.Transient, null, detail),
                _ => new LabelDispatchResult(LabelDispatchOutcome.Invalid, null, detail),
            };
        }
        // Timeout: unknown whether the server accepted the job -> never retry automatically.
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new LabelDispatchResult(LabelDispatchOutcome.Uncertain, null,
                "timeout: esito incerto, il job potrebbe essere già in coda — verificare prima di ripetere");
        }
        // Connection refused / DNS failure: the request never reached the server.
        catch (HttpRequestException ex)
        {
            return new LabelDispatchResult(LabelDispatchOutcome.Unreachable, null, $"server non raggiungibile: {ex.Message}");
        }
    }

    public async Task<LabelServerHealth?> GetHealthAsync(string baseUrl, string? token, CancellationToken ct = default)
    {
        var body = await GetStringAsync(baseUrl, token, "api/v1/health", ct);
        if (body is null) return null;
        var parsed = TryDeserialize<HealthResponse>(body);
        return parsed is null ? null : new LabelServerHealth(parsed.Ok, parsed.Printer, parsed.Pending, parsed.Printing, parsed.Failed);
    }

    public async Task<IReadOnlyList<LabelJob>?> GetJobsAsync(string baseUrl, string? token, string? state = null, int limit = 100, CancellationToken ct = default)
    {
        var path = $"api/v1/jobs?limit={limit}";
        if (!string.IsNullOrWhiteSpace(state))
            path += $"&state={Uri.EscapeDataString(state)}";

        var body = await GetStringAsync(baseUrl, token, path, ct);
        if (body is null) return null;
        var parsed = TryDeserialize<List<JobResponse>>(body);
        return parsed?.Select(ToJob).ToList();
    }

    public async Task<LabelJob?> RetryJobAsync(string baseUrl, string? token, string jobId, CancellationToken ct = default)
    {
        using var client = CreateClient(baseUrl, token);
        try
        {
            using var response = await client.PostAsync($"api/v1/jobs/{Uri.EscapeDataString(jobId)}/retry", null, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return null;
            var parsed = TryDeserialize<JobResponse>(body);
            return parsed is null ? null : ToJob(parsed);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LabelFormat>?> GetFormatsAsync(string baseUrl, string? token, CancellationToken ct = default)
    {
        var body = await GetStringAsync(baseUrl, token, "api/v1/formats", ct);
        if (body is null) return null;
        var parsed = TryDeserialize<FormatsResponse>(body);
        return parsed?.Formats.Select(f => new LabelFormat(f.Name, f.Label)).ToList();
    }

    private HttpClient CreateClient(string baseUrl, string? token)
    {
        var client = _httpClientFactory.CreateClient("LabelServer");
        client.BaseAddress = new Uri(NormalizeBaseUrl(baseUrl), UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(_settings.ServerTimeoutSeconds > 0 ? _settings.ServerTimeoutSeconds : 15);
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string NormalizeBaseUrl(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

    private async Task<string?> GetStringAsync(string baseUrl, string? token, string path, CancellationToken ct)
    {
        using var client = CreateClient(baseUrl, token);
        try
        {
            using var response = await client.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the <c>detail</c> field of an error body, flattening both shapes
    /// (string for application errors, list for schema errors).
    /// </summary>
    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return $"HTTP {(int)response.StatusCode}";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind switch
                {
                    JsonValueKind.String => detail.GetString() ?? body,
                    JsonValueKind.Array => string.Join("; ", detail.EnumerateArray()
                        .Select(FormatValidationError)),
                    _ => body,
                };
            }
        }
        catch (JsonException)
        {
            // fall through: not JSON, return the raw body
        }

        return body;
    }

    private static string FormatValidationError(JsonElement element)
    {
        var msg = element.TryGetProperty("msg", out var m) ? m.GetString() : null;
        var loc = element.TryGetProperty("loc", out var l) && l.ValueKind == JsonValueKind.Array
            ? string.Join(".", l.EnumerateArray().Select(x => x.ToString()))
            : null;
        return loc is null ? msg ?? element.ToString() : $"{loc}: {msg}";
    }

    private static T? TryDeserialize<T>(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static LabelJob ToJob(JobResponse j) =>
        new(j.JobId, j.Status, j.Attempts, j.Error, j.Uncertain);

    private sealed record PrintResponse(
        [property: JsonPropertyName("job_id")] string JobId,
        [property: JsonPropertyName("status")] string? Status);

    private sealed record HealthResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("printer")] string? Printer,
        [property: JsonPropertyName("pending")] int Pending,
        [property: JsonPropertyName("printing")] int Printing,
        [property: JsonPropertyName("failed")] int Failed);

    private sealed record JobResponse(
        [property: JsonPropertyName("job_id")] string JobId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("attempts")] int Attempts,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("uncertain")] bool Uncertain);

    private sealed record FormatsResponse(
        [property: JsonPropertyName("formats")] List<FormatEntry> Formats);

    private sealed record FormatEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("label")] string Label);
}
