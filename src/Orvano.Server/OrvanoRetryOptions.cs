namespace Orvano.Server;

/// <summary>
/// The built in retry (spec 0002 LIB-7): GET, HEAD, and OPTIONS requests are retried on 408, 429, 502, 503, 504, and
/// transient network errors, with exponential backoff and full jitter, honoring <c>Retry-After</c>.
/// </summary>
public sealed class OrvanoRetryOptions
{
    /// <summary>The most retries after the first attempt. 0 turns retrying off. Default 2.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>The backoff base: retry <c>n</c> waits a random time up to <c>BaseDelay * 2^n</c>. Default 0.5 s.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The longest <c>Retry-After</c> the SDK waits for; a longer one fails at once. Default 30 s.</summary>
    public TimeSpan MaxRetryAfter { get; set; } = TimeSpan.FromSeconds(30);

    internal OrvanoRetryOptions Validated(string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries, $"{name}.{nameof(MaxRetries)}");
        ArgumentOutOfRangeException.ThrowIfLessThan(BaseDelay, TimeSpan.Zero, $"{name}.{nameof(BaseDelay)}");
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxRetryAfter, TimeSpan.Zero, $"{name}.{nameof(MaxRetryAfter)}");
        // A copy, so a later change to the options can't change a client already built.
        return new OrvanoRetryOptions { MaxRetries = MaxRetries, BaseDelay = BaseDelay, MaxRetryAfter = MaxRetryAfter };
    }
}
