using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Orvano.Server.Http;

/// <summary>The retry rules of spec 0002 LIB-7, shared with the other Orvano SDKs (spec 0014 IDM-3).</summary>
internal static class RetryRules
{
    /// <summary>The statuses worth another attempt.</summary>
    public static readonly IReadOnlySet<int> Statuses = new HashSet<int> { 408, 429, 502, 503, 504 };

    /// <summary>The methods retried without an idempotency key.</summary>
    public static readonly IReadOnlySet<string> Methods = new HashSet<string>(StringComparer.Ordinal) { "GET", "HEAD", "OPTIONS" };

    /// <summary>Exponential backoff with full jitter: a random wait up to <c>baseDelay * 2^attempt</c>.</summary>
    public static TimeSpan Backoff(int attempt, TimeSpan baseDelay)
    {
        // RandomNumberGenerator, since System.Random is banned (BannedSymbols.txt); jitter needs no more than this.
        double fraction = RandomNumberGenerator.GetInt32(int.MaxValue) / (double)int.MaxValue;
        return baseDelay * (fraction * Math.Pow(2, attempt));
    }

    /// <summary><c>Retry-After</c> in seconds or as an HTTP date, as a wait from <paramref name="now"/>; null when absent.</summary>
    public static TimeSpan? RetryAfter(RetryConditionHeaderValue? header, DateTimeOffset now) => header switch
    {
        { Delta: { } delta } => delta < TimeSpan.Zero ? null : delta,
        { Date: { } date } => date > now ? date - now : TimeSpan.Zero,
        _ => null,
    };

    /// <summary>A failure worth another attempt: not a refused redirect, and not a TLS failure, which won't heal.</summary>
    public static bool IsTransient(HttpRequestException exception) =>
        exception is not RedirectRefusedException && exception.HttpRequestError != HttpRequestError.SecureConnectionError;
}
