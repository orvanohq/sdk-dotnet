using System.Globalization;

namespace Orvano.Server;

/// <summary>
/// The server answered with an error (spec 0002 LIB-6). <see cref="Code"/> says which; compare it with the constants in
/// <see cref="OrvanoErrorCodes"/>.
/// </summary>
public class OrvanoException : Exception
{
    private const string DefaultMessage = "The Orvano server answered with an error.";

    /// <summary>Creates an exception with no response, for tests.</summary>
    public OrvanoException()
        : this(DefaultMessage)
    {
    }

    /// <summary>Creates an exception with no response, for tests.</summary>
    /// <param name="message">The message.</param>
    public OrvanoException(string message)
        : this(message, null)
    {
    }

    /// <summary>Creates an exception with no response, for tests.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public OrvanoException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Kind = OrvanoErrorKind.Server;
        Code = OrvanoErrorCodes.Unknown;
        Title = message;
        RawBody = string.Empty;
    }

    /// <summary>Creates the exception for an error response.</summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="title">The problem's title, else the reason phrase.</param>
    /// <param name="problem">The problem body, or null when the body was not one.</param>
    /// <param name="traceId">The <c>Orvano-Request-Id</c> header, else the problem's <c>traceId</c>.</param>
    /// <param name="rawBody">At most 4 KB of a body that was not a problem; empty otherwise.</param>
    public OrvanoException(int statusCode, string title, OrvanoProblem? problem, string? traceId, string rawBody)
        : base(MessageFor(statusCode, problem?.Code ?? OrvanoErrorCodes.Unknown, problem?.Detail ?? title))
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(rawBody);
        Kind = KindFor(statusCode);
        StatusCode = statusCode;
        Code = problem?.Code ?? OrvanoErrorCodes.Unknown;
        Title = title;
        Detail = problem?.Detail;
        TraceId = traceId;
        Problem = problem;
        RawBody = rawBody;
    }

    /// <summary>What went wrong, from the status alone (spec 0014 KND-2).</summary>
    public OrvanoErrorKind Kind { get; }

    /// <summary>The HTTP status.</summary>
    public int StatusCode { get; }

    /// <summary>The error code, or <see cref="OrvanoErrorCodes.Unknown"/> when the body was not a problem.</summary>
    public string Code { get; }

    /// <summary>The problem's title, else the HTTP reason phrase.</summary>
    public string Title { get; }

    /// <summary>An English message safe to show, when the server sent one.</summary>
    public string? Detail { get; }

    /// <summary>The request ID (<c>Orvano-Request-Id</c>), to find the request in the server's logs.</summary>
    public string? TraceId { get; }

    /// <summary>The problem body, or null when the body was not one.</summary>
    public OrvanoProblem? Problem { get; }

    /// <summary>At most 4 KB of a body that was not a problem (spec 0014 KND-3); empty otherwise.</summary>
    public string RawBody { get; }

    /// <summary>On <c>validation_failed</c>: each field that is not valid.</summary>
    public IReadOnlyList<OrvanoFieldError> FieldErrors => Problem?.Errors ?? [];

    /// <summary>The kind for an HTTP status (spec 0014 KND-2).</summary>
    internal static OrvanoErrorKind KindFor(int statusCode) => statusCode switch
    {
        400 or 413 or 415 or 422 => OrvanoErrorKind.Validation,
        401 => OrvanoErrorKind.Unauthenticated,
        403 => OrvanoErrorKind.Forbidden,
        404 => OrvanoErrorKind.NotFound,
        408 => OrvanoErrorKind.Timeout,
        409 or 412 or 428 => OrvanoErrorKind.Conflict,
        429 => OrvanoErrorKind.RateLimited,
        _ => OrvanoErrorKind.Server,
    };

    private static string MessageFor(int statusCode, string code, string text) =>
        string.Create(CultureInfo.InvariantCulture, $"{statusCode} {code}: {text}");
}
