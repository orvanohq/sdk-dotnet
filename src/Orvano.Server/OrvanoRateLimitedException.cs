namespace Orvano.Server;

/// <summary>The server answered 429: too many requests (spec 0002 LIB-6).</summary>
public sealed class OrvanoRateLimitedException : OrvanoException
{
    /// <summary>Creates an exception with no response, for tests.</summary>
    public OrvanoRateLimitedException()
    {
    }

    /// <summary>Creates an exception with no response, for tests.</summary>
    /// <param name="message">The message.</param>
    public OrvanoRateLimitedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with no response, for tests.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public OrvanoRateLimitedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a 429 response.</summary>
    /// <param name="title">The problem's title, else the reason phrase.</param>
    /// <param name="problem">The problem body, or null when the body was not one.</param>
    /// <param name="traceId">The <c>Orvano-Request-Id</c> header, else the problem's <c>traceId</c>.</param>
    /// <param name="rawBody">At most 4 KB of a body that was not a problem; empty otherwise.</param>
    /// <param name="retryAfter">How long the server asked you to wait, from <c>Retry-After</c> or the body.</param>
    public OrvanoRateLimitedException(string title, OrvanoProblem? problem, string? traceId, string rawBody, TimeSpan? retryAfter)
        : base(429, title, problem, traceId, rawBody)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long the server asked you to wait before trying again, when it said.</summary>
    public TimeSpan? RetryAfter { get; }
}
