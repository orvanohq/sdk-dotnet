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
    /// <c>Idempotency-Key</c>, <c>If-Match</c>, and <c>User-Agent</c> are the SDK's and can't be set here.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    private static readonly System.Buffers.SearchValues<char> NotInVersion = System.Buffers.SearchValues.Create("\" ,\t\r\n");

    /// <summary>The version a generated write sends as <c>If-Match</c> (spec 0014 CNC-4), from its <c>ifMatch</c> parameter.</summary>
    internal string? IfMatch { get; init; }

    /// <summary>
    /// <paramref name="options"/> with <paramref name="version"/> to send as <c>If-Match</c>; the generated services
    /// call this for their <c>ifMatch</c> parameter. A null <paramref name="version"/> leaves the options as they are.
    /// </summary>
    internal static OrvanoRequestOptions? WithIfMatch(OrvanoRequestOptions? options, string? version)
    {
        if (version is null)
        {
            return options;
        }

        if (version.Length == 0 || version.AsSpan().IndexOfAny(NotInVersion) >= 0)
        {
            throw new ArgumentException("ifMatch must be a resource's version, as its Version property holds it.", nameof(version));
        }

        return new OrvanoRequestOptions
        {
            Timeout = options?.Timeout,
            Retry = options?.Retry,
            Headers = options?.Headers,
            IfMatch = version,
            IdempotencyKey = options?.IdempotencyKey,
        };
    }

    /// <summary>The key a generated call sends as <c>Idempotency-Key</c> (spec 0014 IDM-1), from its <c>idempotencyKey</c> parameter.</summary>
    internal string? IdempotencyKey { get; init; }

    /// <summary>
    /// <paramref name="options"/> with <paramref name="key"/> to send as <c>Idempotency-Key</c>, which makes the call
    /// retryable (IDM-3); the generated services call this for marked operations' <c>idempotencyKey</c> parameter. A
    /// null <paramref name="key"/> leaves the options as they are.
    /// </summary>
    internal static OrvanoRequestOptions? WithIdempotencyKey(OrvanoRequestOptions? options, string? key)
    {
        if (key is null)
        {
            return options;
        }

        if (key.Length is 0 or > 255 || key.AsSpan().IndexOfAnyExceptInRange(' ', '~') >= 0)
        {
            throw new ArgumentException("idempotencyKey must be 1 to 255 printable ASCII characters.", nameof(key));
        }

        return new OrvanoRequestOptions
        {
            Timeout = options?.Timeout,
            Retry = options?.Retry,
            Headers = options?.Headers,
            IfMatch = options?.IfMatch,
            IdempotencyKey = key,
        };
    }
}
