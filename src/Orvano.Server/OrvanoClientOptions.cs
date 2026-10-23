using Microsoft.Extensions.Logging;

namespace Orvano.Server;

/// <summary>How an <see cref="OrvanoClient"/> reaches its project (spec 0002 LIB-4, LIB-7, LIB-8).</summary>
public sealed class OrvanoClientOptions
{
    /// <summary>
    /// Your Orvano server, such as <c>https://orvano.example.com</c>. It must be https, except for a loopback or
    /// <c>.localhost</c> host, unless <see cref="AllowInsecureEndpoint"/> is set.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>The project every data call goes to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The project's secret key (<c>orv_sk_…</c>). Keep it on your server; it never appears in logs.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The timeout for each attempt. Default 30 s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The retry settings (spec 0002 LIB-7). <c>Retry.MaxRetries = 0</c> turns retrying off.</summary>
    public OrvanoRetryOptions Retry { get; set; } = new();

    /// <summary>Allows an http endpoint on a host that is not loopback. Leave it off outside local development.</summary>
    public bool AllowInsecureEndpoint { get; set; }

    /// <summary>The clock for timeouts and backoff. Tests can pass a fake one.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Logs each call's method, path, status, and duration at Debug, never bodies or the key.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>The endpoint and project, never the key (spec 0002 LIB-8).</summary>
    /// <returns>A description safe to log.</returns>
    public override string ToString() =>
        $"OrvanoClientOptions {{ Endpoint = {Endpoint}, ProjectId = {ProjectId}, ApiKey = <redacted> }}";
}
