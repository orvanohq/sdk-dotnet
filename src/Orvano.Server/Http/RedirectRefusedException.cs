using System.Globalization;
using System.Net;

namespace Orvano.Server.Http;

/// <summary>
/// The SDK's own failure for a 3xx: a credential never follows a redirect to another host (spec 0014 PTH-4). It is an
/// <see cref="HttpRequestException"/>, as LIB-6 says a failed request is, and it is never retried.
/// </summary>
internal sealed class RedirectRefusedException(int status)
    : HttpRequestException(
        string.Create(CultureInfo.InvariantCulture, $"The server answered {status}, a redirect, which the SDK never follows."),
        inner: null,
        (HttpStatusCode)status);
