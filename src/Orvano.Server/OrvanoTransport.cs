using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Logging;

using Orvano.Server.Http;

namespace Orvano.Server;

/// <summary>
/// The one HTTP path every call takes (spec 0002 LIB-5 to LIB-8): a fresh request per attempt, the per attempt
/// timeout, retries with backoff or <c>Retry-After</c>, and error mapping. The generated services hand it an
/// <see cref="OrvanoCall"/> (spec 0014 GNR-5).
/// </summary>
internal sealed class OrvanoTransport
{
    private static readonly string[] ReservedHeaders =
    [
        "Authorization",
        OrvanoHeaders.OrvanoKey,
        OrvanoHeaders.OrvanoClient,
        OrvanoHeaders.IdempotencyKey,
        OrvanoHeaders.IfMatch,
        "User-Agent",
    ];

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly TimeSpan _timeout;
    private readonly OrvanoRetryOptions _retry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public OrvanoTransport(OrvanoClientOptions options, HttpClient http, ILogger logger)
    {
        _endpoint = CheckEndpoint(options.Endpoint, options.AllowInsecureEndpoint);
        if (options.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId must be set.", nameof(options));
        }
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("ApiKey must be set.", nameof(options));
        }
        ArgumentNullException.ThrowIfNull(options.Retry, "options.Retry");
        ArgumentNullException.ThrowIfNull(options.TimeProvider, "options.TimeProvider");
        _timeout = CheckTimeout(options.Timeout, "options.Timeout");
        _retry = options.Retry.Validated("options.Retry");
        _timeProvider = options.TimeProvider;
        _apiKey = options.ApiKey;
        _http = http;
        _logger = logger;
        ProjectId = OrvanoWire.Text(options.ProjectId);
    }

    /// <summary>The value of <c>Orvano-Client</c> and <c>User-Agent</c> (spec 0014 HDR-3).</summary>
    public static string ClientName { get; } =
        $"Orvano.Server/{typeof(OrvanoTransport).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}";

    /// <summary>The project the data plane calls go to (spec 0014 PTH-5).</summary>
    public string ProjectId { get; }

    /// <summary>Sends a call and reads its JSON body.</summary>
    public async Task<T> SendAsync<T>(OrvanoCall call, JsonTypeInfo<T> responseType, OrvanoRequestOptions? options, CancellationToken cancellationToken)
    {
        byte[] body = await SendCoreAsync(call, options, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, responseType)
            ?? throw new JsonException($"{call.OperationId} answered null, which its contract does not allow.");
    }

    /// <summary>Sends a call whose answer has no body.</summary>
    public async Task SendAsync(OrvanoCall call, OrvanoRequestOptions? options, CancellationToken cancellationToken) =>
        await SendCoreAsync(call, options, cancellationToken).ConfigureAwait(false);

    /// <summary>The endpoint and project, never the key (spec 0002 LIB-8).</summary>
    public override string ToString() => $"OrvanoClient {{ Endpoint = {_endpoint}, ProjectId = {ProjectId} }}";

    private async Task<byte[]> SendCoreAsync(OrvanoCall call, OrvanoRequestOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        TimeSpan timeout = options?.Timeout is { } callTimeout ? CheckTimeout(callTimeout, "options.Timeout") : _timeout;
        OrvanoRetryOptions retry = options?.Retry?.Validated("options.Retry") ?? _retry;
        List<KeyValuePair<string, string>> headers = CheckHeaders(options?.Headers);
        if (options?.IfMatch is { } version)
        {
            // CNC-3, CNC-4: the version the caller read, as a strong ETag.
            headers.Add(new(OrvanoHeaders.IfMatch, $"\"{version}\""));
        }

        if (options?.IdempotencyKey is { } key)
        {
            // IDM-1: the caller's key, on operations marked for it.
            headers.Add(new(OrvanoHeaders.IdempotencyKey, key));
        }
        var uri = new Uri(_endpoint, RelativeUri(call));
        // IDM-3: a non GET call is retried only with an idempotency key, which only marked operations take.
        bool retriable = RetryRules.Methods.Contains(call.Method.Method) || options?.IdempotencyKey is not null;

        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan? wait = null;
            // ASY-7: the attempt timeout comes from the TimeProvider, linked to the caller's token.
            using (var attemptTimeout = new CancellationTokenSource(timeout, _timeProvider))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptTimeout.Token))
            {
                long started = _timeProvider.GetTimestamp();
                try
                {
                    // LIB-7: a sent message can't be sent again, so every attempt builds a fresh one.
                    using HttpRequestMessage request = BuildRequest(call, uri, headers);
                    using HttpResponseMessage response = await _http
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                        .ConfigureAwait(false);
                    int status = (int)response.StatusCode;
                    if (status is >= 300 and < 400)
                    {
                        // A credential never follows a redirect to another host (spec 0014 PTH-4).
                        throw new RedirectRefusedException(status);
                    }
                    byte[] body = await response.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        TimeSpan elapsed = _timeProvider.GetElapsedTime(started);
                        Log.Call(_logger, call.Method.Method, uri.AbsolutePath, status, elapsed.TotalMilliseconds);
                    }
                    if (status is >= 200 and < 300)
                    {
                        return body;
                    }
                    OrvanoException error = ProblemReader.ErrorFrom(response, body, _timeProvider.GetUtcNow());
                    if (!retriable || attempt >= retry.MaxRetries || !RetryRules.Statuses.Contains(status))
                    {
                        throw error;
                    }
                    TimeSpan? retryAfter = RetryRules.RetryAfter(response.Headers.RetryAfter, _timeProvider.GetUtcNow());
                    if (retryAfter > retry.MaxRetryAfter)
                    {
                        throw error;
                    }
                    wait = retryAfter;
                }
                catch (OperationCanceledException) when (attemptTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    Log.TimedOut(_logger, call.Method.Method, uri.AbsolutePath, timeout.TotalMilliseconds);
                    if (!retriable || attempt >= retry.MaxRetries)
                    {
                        // LIB-6: the last attempt's timeout is a TimeoutException, never the caller's cancellation.
                        throw new TimeoutException(
                            string.Create(CultureInfo.InvariantCulture, $"{call.OperationId} took longer than {timeout.TotalSeconds:0.###} s."));
                    }
                }
                catch (HttpRequestException exception) when (retriable && attempt < retry.MaxRetries && RetryRules.IsTransient(exception))
                {
                    Log.Failed(_logger, call.Method.Method, uri.AbsolutePath);
                }
            }
            await Task.Delay(wait ?? RetryRules.Backoff(attempt, retry.BaseDelay), _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private HttpRequestMessage BuildRequest(OrvanoCall call, Uri uri, IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var request = new HttpRequestMessage(call.Method, uri);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.TryAddWithoutValidation(OrvanoHeaders.OrvanoApiVersion, OrvanoContract.ApiVersion);
        foreach ((string? name, string? value) in headers)
        {
            request.Headers.Remove(name);
            request.Headers.TryAddWithoutValidation(name, value);
        }
        // Set last, so no caller header can replace them.
        request.Headers.TryAddWithoutValidation(OrvanoHeaders.OrvanoClient, ClientName);
        request.Headers.TryAddWithoutValidation("User-Agent", ClientName);
        request.Headers.TryAddWithoutValidation(OrvanoHeaders.OrvanoKey, _apiKey);
        if (call.Body is { } body)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
        }
        return request;
    }

    /// <summary>The path and query, each part escaped, relative to the endpoint.</summary>
    private static string RelativeUri(OrvanoCall call)
    {
        var builder = new StringBuilder();
        builder.AppendJoin('/', call.Path.Select(static segment => Uri.EscapeDataString(CheckSegment(segment))));
        for (int index = 0; index < call.Query.Count; index++)
        {
            (string? name, string? value) = call.Query[index];
            builder.Append(index == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Refuses an empty or dot segment before any request (LIB-6, spec 0014 PTH-4): <c>Uri</c> removes <c>.</c> and
    /// <c>..</c> (also as <c>%2e</c>), which would send the call to another route.
    /// </summary>
    private static string CheckSegment(string segment)
    {
        ArgumentException.ThrowIfNullOrEmpty(segment);
        string decoded = segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);
        if (decoded is "." or "..")
        {
            throw new ArgumentException("A path value must not be \".\" or \"..\".", nameof(segment));
        }

        return segment;
    }

    private static List<KeyValuePair<string, string>> CheckHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        var checkedHeaders = new List<KeyValuePair<string, string>>();
        foreach ((string? name, string? value) in headers ?? new Dictionary<string, string>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(headers));
            ArgumentNullException.ThrowIfNull(value, nameof(headers));
            if (ReservedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{name} is set by the SDK and can't be set here.", nameof(headers));
            }
            checkedHeaders.Add(new(name, value));
        }
        return checkedHeaders;
    }

    private static TimeSpan CheckTimeout(TimeSpan timeout, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, name);
        return timeout;
    }

    /// <summary>Checks the endpoint and returns it ending in <c>/</c>, so request paths resolve under it.</summary>
    private static Uri CheckEndpoint(Uri? endpoint, bool allowInsecure)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("Endpoint must be an absolute URL.", nameof(endpoint));
        }
        if (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException("Endpoint must be http or https.", nameof(endpoint));
        }
        if (endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
        {
            throw new ArgumentException("Endpoint must not carry a user, a query, or a fragment.", nameof(endpoint));
        }
        string host = endpoint.IdnHost;
        bool local = endpoint.IsLoopback || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
        if (endpoint.Scheme == Uri.UriSchemeHttp && !local && !allowInsecure)
        {
            throw new ArgumentException("Endpoint must be https; set AllowInsecureEndpoint to allow http for this host.", nameof(endpoint));
        }
        string text = endpoint.GetLeftPart(UriPartial.Path);
        return new Uri(text.EndsWith('/') ? text : $"{text}/", UriKind.Absolute);
    }
}
