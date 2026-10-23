using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Orvano.Server;

/// <summary>
/// The Orvano server SDK's client: thread safe and meant to be reused (spec 0002 LIB-4). The services hang off it as
/// properties, such as <see cref="OrvanoServices.Health"/>.
/// </summary>
/// <remarks>
/// Create one with <c>new OrvanoClient(options)</c>, or register it with
/// <see cref="OrvanoServiceCollectionExtensions.AddOrvano"/>. An <see cref="HttpClient"/> you pass in stays yours: the
/// client never disposes it. Turn off redirects on its handler; the SDK never follows one and treats a 3xx as a failed
/// request, so a key never reaches another host.
/// </remarks>
public sealed class OrvanoClient : OrvanoServices
{
    // LIB-4: the one static. PooledConnectionLifetime lets DNS changes be seen; redirects are never followed.
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
    };

    /// <summary>Creates a client.</summary>
    /// <param name="options">The endpoint, project, key, and call settings.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to send with, or null for the SDK's shared handler.</param>
    /// <exception cref="ArgumentException">An option is missing or not valid.</exception>
    public OrvanoClient(OrvanoClientOptions options, HttpClient? httpClient = null)
        : base(CreateTransport(options, httpClient, options?.LoggerFactory))
    {
    }

    /// <summary>The typed client <see cref="OrvanoServiceCollectionExtensions.AddOrvano"/> registers.</summary>
    internal OrvanoClient(OrvanoClientOptions options, HttpClient httpClient, ILoggerFactory? loggerFactory)
        : base(CreateTransport(options, httpClient, loggerFactory))
    {
    }

    /// <summary>The endpoint and project, never the key (spec 0002 LIB-8).</summary>
    /// <returns>A description safe to log.</returns>
    public override string ToString() => Transport.ToString();

    private static OrvanoTransport CreateTransport(OrvanoClientOptions? options, HttpClient? httpClient, ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        HttpClient http = httpClient ?? new HttpClient(SharedHandler, disposeHandler: false)
        {
            // Each attempt has its own timeout (LIB-7), so HttpClient's must never cut in first.
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
        ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger("Orvano.Server");
        return new OrvanoTransport(options, http, logger);
    }
}
