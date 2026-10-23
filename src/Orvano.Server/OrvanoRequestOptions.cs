namespace Orvano.Server;

/// <summary>Options for one call (spec 0002 LIB-5). Anything left null uses the client's setting.</summary>
public sealed class OrvanoRequestOptions
{
    /// <summary>The timeout for each attempt, for example longer for an upload.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>The retry settings for this call.</summary>
    public OrvanoRetryOptions? Retry { get; init; }

    /// <summary>
    /// Extra headers for this call. <c>Authorization</c>, <c>Orvano-Key</c>, <c>Orvano-Client</c>,
    /// <c>Idempotency-Key</c>, and <c>User-Agent</c> are the SDK's and can't be set here.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
