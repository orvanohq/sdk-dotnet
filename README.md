# Orvano .NET Server SDK

`Orvano.Server` calls your [Orvano](https://github.com/orvanohq/orvano) project from .NET on a server: an ASP.NET Core app, a worker, or a CLI. Orvano is an open source, self hostable backend as a service. It holds a secret key, so keep it on your server.

## Install

```bash
dotnet add package Orvano.Server
```

It targets `net10.0`, is trim and AOT safe, and depends only on `Microsoft.Extensions.Http`.

## Quick start

```csharp
using Orvano.Server;

var orvano = new OrvanoClient(new OrvanoClientOptions
{
    Endpoint = new Uri("https://orvano.example.com"),
    ProjectId = Guid.Parse("0192f0c1-0000-7000-8000-000000000001"),
    ApiKey = Environment.GetEnvironmentVariable("ORVANO_SECRET_KEY"), // never commit it
});

try
{
    var health = await orvano.Health.GetAsync(cancellationToken: cancellationToken);
    Console.WriteLine(health.Status);
}
catch (OrvanoException error) when (error.Code == OrvanoErrorCodes.Unavailable)
{
    // the server is not ready
}
```

`OrvanoClient` is thread safe: create one and reuse it. With dependency injection, register it as a typed client instead:

```csharp
builder.Services.AddOrvano(options =>
{
    options.Endpoint = new Uri(builder.Configuration["Orvano:Endpoint"]!);
    options.ProjectId = Guid.Parse(builder.Configuration["Orvano:ProjectId"]!);
    options.ApiKey = builder.Configuration["Orvano:SecretKey"];
});
```

## Errors

- The server answered with an error: `OrvanoException`, with `StatusCode`, `Kind` (from the status alone, the same in every Orvano SDK), `Code` (compare it with `OrvanoErrorCodes`), `Detail`, `TraceId`, `FieldErrors`, and the `Problem` body. A 429 is an `OrvanoRateLimitedException` with `RetryAfter`.
- The connection failed, or the server answered with a redirect: `HttpRequestException`.
- The last attempt ran out of time: `TimeoutException`. Your own cancellation stays an `OperationCanceledException`.
- A bad argument: `ArgumentException`, before any request.

## Timeouts and retries

Each attempt has 30 seconds (`OrvanoClientOptions.Timeout`, or per call with `OrvanoRequestOptions.Timeout`). GET requests are retried up to 2 times after a 408, 429, 502, 503, or 504, or a dropped connection, with backoff and full jitter, or after the server's `Retry-After` (up to 30 seconds; a longer one throws). Other methods are never retried. `Retry.MaxRetries = 0` turns retries off.

## Security notes

- The endpoint must be `https`, except for a loopback or `*.localhost` host. Set `AllowInsecureEndpoint` for a server on your own network.
- The SDK never follows a redirect, so your key never reaches another host. If you pass your own `HttpClient`, turn off `AllowAutoRedirect` on its handler too; the SDK still refuses a 3xx.
- No exception, log line, or `ToString` shows your key. Logs (category `Orvano.Server`, level Debug) show the method, path, status, and duration only.
- An `HttpClient` you pass in stays yours: the client never disposes it.

## Working on this repo

`src/Orvano.Server/Generated/` and `tests/Orvano.Server.ConformanceTests/Generated/` come from the orvano generator at the commit in `orvano-contract.json` (spec 0014). Never edit them by hand; everything else is hand written.

- Regenerate from the pin: `tool/regenerate.sh` (needs git, Node 24, pnpm through `corepack enable`, and the .NET SDK). Move the pin with `tool/regenerate.sh --pin <orvano commit>`. CI regenerates and fails on any difference.
- Build and unit tests: `dotnet build Orvano.Server.slnx`, then `dotnet test`.
- Conformance, against Orvano and the stub at the pinned commit:

  ```bash
  tool/regenerate.sh --fetch-only
  docker build --file .orvano/deploy/Dockerfile --tag orvano:pinned .orvano
  ORVANO_IMAGE=orvano ORVANO_IMAGE_TAG=pinned docker compose --file .orvano/api/conformance/compose.yaml up --detach --wait
  dotnet run --project tests/Orvano.Server.ConformanceTests
  ```

  A scenario that requires a project gets one from `orvano admin bootstrap-project`, run through `docker compose exec` in that stack (spec 0017 SDK-4). `dotnet test` leaves this project out, since it needs the servers. Set `ORVANO_CONFORMANCE_URL` and `ORVANO_CONFORMANCE_STUB_URL` for other ports, and `ORVANO_CONFORMANCE_PARTIAL=1` when you run a few scenarios with a filter.

## License

Apache License 2.0 (see `LICENSE` and `NOTICE`). The name "Orvano" and its logo aren't covered by the license;
see [TRADEMARKS.md](https://github.com/orvanohq/orvano/blob/main/TRADEMARKS.md).
