using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orvano.Server;

/// <summary>Registers <see cref="OrvanoClient"/> with dependency injection (spec 0002 LIB-4).</summary>
public static class OrvanoServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OrvanoClient"/> as a typed client through <see cref="IHttpClientFactory"/>, configured by
    /// <paramref name="configure"/>. Without a logger factory in the options, it logs through the container's.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets the endpoint, project, key, and call settings.</param>
    /// <returns>The HTTP client builder, to add handlers or change the primary handler.</returns>
    public static IHttpClientBuilder AddOrvano(this IServiceCollection services, Action<OrvanoClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<OrvanoClientOptions>().Configure(configure);
        return services
            .AddHttpClient(nameof(OrvanoClient))
            .AddTypedClient((http, provider) =>
            {
                OrvanoClientOptions options = provider.GetRequiredService<IOptions<OrvanoClientOptions>>().Value;
                return new OrvanoClient(options, http, options.LoggerFactory ?? provider.GetService<ILoggerFactory>());
            })
            .ConfigureHttpClient(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
            });
    }
}
