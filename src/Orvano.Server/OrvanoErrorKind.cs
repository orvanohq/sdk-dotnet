namespace Orvano.Server;

/// <summary>
/// What went wrong, read from the HTTP status alone, the same way in every Orvano SDK (spec 0014 KND-2).
/// </summary>
/// <remarks>
/// A timeout of the SDK's own attempt is a <see cref="TimeoutException"/>, and a failed connection an
/// <see cref="HttpRequestException"/>; neither has a status.
/// </remarks>
public enum OrvanoErrorKind
{
    /// <summary>400, 413, 415, or 422: the request is not valid.</summary>
    Validation,

    /// <summary>401: the key is missing or not valid.</summary>
    Unauthenticated,

    /// <summary>403: the key may not do this.</summary>
    Forbidden,

    /// <summary>404: there is no such resource, or the key may not see it.</summary>
    NotFound,

    /// <summary>408: the server gave up waiting for the request.</summary>
    Timeout,

    /// <summary>409, 412, or 428: the request conflicts with the resource's state.</summary>
    Conflict,

    /// <summary>429: too many requests; see <see cref="OrvanoRateLimitedException.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>Any other 4xx or 5xx.</summary>
    Server,
}
