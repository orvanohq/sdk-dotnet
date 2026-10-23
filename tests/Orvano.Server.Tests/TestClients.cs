namespace Orvano.Server.Tests;

internal static class TestClients
{
    public const string Key = "orv_sk_test_not_real";

    public static readonly Guid ProjectId = Guid.Parse("0192f0c1-0000-7000-8000-000000000001");

    public static OrvanoClientOptions Options(Action<OrvanoClientOptions>? configure = null)
    {
        var options = new OrvanoClientOptions
        {
            Endpoint = new Uri("https://orvano.example.test"),
            ProjectId = ProjectId,
            ApiKey = Key,
            Retry = new OrvanoRetryOptions { BaseDelay = TimeSpan.Zero },
        };
        configure?.Invoke(options);
        return options;
    }

    public static OrvanoClient Client(StubHandler handler, Action<OrvanoClientOptions>? configure = null) =>
        new(Options(configure), new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });

    public const string HealthBody = """{"status":"ok","apiVersions":["2026-10-22"]}""";

    public static string Problem(int status, string code, string extra = "") =>
        $$"""{"type":"about:blank","title":"T","status":{{status}},"detail":"D","code":"{{code}}","traceId":"0af7651916cd43dd8448eb211c80319c"{{extra}}}""";
}
