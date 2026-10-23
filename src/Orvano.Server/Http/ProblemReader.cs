using System.Text;
using System.Text.Json;

namespace Orvano.Server.Http;

/// <summary>Turns an error response into an <see cref="OrvanoException"/> (spec 0014 PRB-1, KND-2, KND-3).</summary>
internal static class ProblemReader
{
    private const int RawBodyLimit = 4096;

    public static OrvanoException ErrorFrom(HttpResponseMessage response, byte[] body, DateTimeOffset now)
    {
        int status = (int)response.StatusCode;
        OrvanoProblem? problem = ProblemOf(body);
        string title = problem?.Title ?? (string.IsNullOrEmpty(response.ReasonPhrase) ? $"HTTP {status}" : response.ReasonPhrase);
        string? traceId = response.Headers.TryGetValues(OrvanoHeaders.OrvanoRequestId, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
        traceId ??= problem?.TraceId;
        string rawBody = problem is not null ? string.Empty : RawText(body);
        if (status == 429)
        {
            TimeSpan? retryAfter = RetryRules.RetryAfter(response.Headers.RetryAfter, now)
                ?? (problem?.RetryAfter is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
            return new OrvanoRateLimitedException(title, problem, traceId, rawBody, retryAfter);
        }
        return new OrvanoException(status, title, problem, traceId, rawBody);
    }

    /// <summary>The problem body, or null when the body is not one: a JSON object with a string code and a number status.</summary>
    internal static OrvanoProblem? ProblemOf(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || String(root, "code") is not { } code
                || !root.TryGetProperty("status", out JsonElement status)
                || !status.TryGetInt32(out int statusCode))
            {
                return null;
            }
            return new OrvanoProblem
            {
                Type = String(root, "type"),
                Title = String(root, "title"),
                Status = statusCode,
                Detail = String(root, "detail"),
                Code = code,
                TraceId = String(root, "traceId"),
                Errors = FieldErrors(root),
                RetryAfter = root.TryGetProperty("retryAfter", out JsonElement retryAfter) && retryAfter.TryGetInt32(out int seconds) && seconds >= 0
                    ? seconds
                    : null,
                SupportedVersions = root.TryGetProperty("supportedVersions", out JsonElement versions) && versions.ValueKind == JsonValueKind.Array
                    ? [.. versions.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString() ?? string.Empty)]
                    : null,
                CurrentVersion = String(root, "currentVersion"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<OrvanoFieldError> FieldErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out JsonElement errors) || errors.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return
        [
            .. errors.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.Object && String(item, "field") is not null && String(item, "code") is not null)
                .Select(static item => new OrvanoFieldError(
                    String(item, "field") ?? string.Empty,
                    String(item, "location") ?? "body",
                    String(item, "code") ?? string.Empty,
                    String(item, "message") ?? string.Empty)),
        ];
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string RawText(byte[] body)
    {
        string text = Encoding.UTF8.GetString(body);
        return text.Length <= RawBodyLimit ? text : text[..RawBodyLimit];
    }
}
