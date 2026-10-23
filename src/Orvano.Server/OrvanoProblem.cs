namespace Orvano.Server;

/// <summary>The error body the server sent (RFC 9457 problem details, spec 0014 PRB-1).</summary>
public sealed record OrvanoProblem
{
    /// <summary>Always <c>about:blank</c>: the code is the error's identity.</summary>
    public string? Type { get; init; }

    /// <summary>The HTTP reason phrase, such as <c>Not Found</c>.</summary>
    public string? Title { get; init; }

    /// <summary>The HTTP status.</summary>
    public int Status { get; init; }

    /// <summary>An English message safe to show.</summary>
    public string? Detail { get; init; }

    /// <summary>The snake_case error code; <see cref="OrvanoErrorCodes"/> lists the known ones.</summary>
    public required string Code { get; init; }

    /// <summary>Equals the <c>Orvano-Request-Id</c> header.</summary>
    public string? TraceId { get; init; }

    /// <summary>On <c>validation_failed</c>: each field that is not valid.</summary>
    public IReadOnlyList<OrvanoFieldError> Errors { get; init; } = [];

    /// <summary>On a 429 or a 503 that knows when to come back: seconds to wait.</summary>
    public int? RetryAfter { get; init; }

    /// <summary>On <c>unsupported_api_version</c>: the versions the server serves.</summary>
    public IReadOnlyList<string>? SupportedVersions { get; init; }

    /// <summary>On <c>version_conflict</c>: the resource's current version.</summary>
    public string? CurrentVersion { get; init; }
}
