using System.Net;

using Microsoft.Extensions.DependencyInjection;

namespace Orvano.Server.Tests;

public sealed class OrvanoClientTests
{
    [Theory]
    [InlineData("http://orvano.example.test")]
    [InlineData("ftp://orvano.example.test")]
    [InlineData("https://user@orvano.example.test")]
    [InlineData("https://orvano.example.test/?a=1")]
    [Trait("Spec", "0002:LIB-6")]
    public void Constructor_throws_ArgumentException_when_the_endpoint_is_not_allowed(string endpoint)
    {
        Should.Throw<ArgumentException>(() => new OrvanoClient(TestClients.Options(options => options.Endpoint = new Uri(endpoint))));
    }

    [Theory]
    [InlineData("http://localhost:18080")]
    [InlineData("http://127.0.0.1:18080")]
    [InlineData("http://orvano.localhost")]
    public void Constructor_allows_http_when_the_host_is_local(string endpoint)
    {
        Should.NotThrow(() => new OrvanoClient(TestClients.Options(options => options.Endpoint = new Uri(endpoint))));
    }

    [Fact]
    public void Constructor_allows_http_when_AllowInsecureEndpoint_is_set()
    {
        Should.NotThrow(() => new OrvanoClient(TestClients.Options(options =>
        {
            options.Endpoint = new Uri("http://orvano.internal");
            options.AllowInsecureEndpoint = true;
        })));
    }

    [Fact]
    public void Constructor_throws_ArgumentException_when_the_key_is_missing()
    {
        Should.Throw<ArgumentException>(() => new OrvanoClient(TestClients.Options(options => options.ApiKey = "")));
    }

    [Fact]
    public void Constructor_throws_ArgumentException_when_the_project_is_missing()
    {
        Should.Throw<ArgumentException>(() => new OrvanoClient(TestClients.Options(options => options.ProjectId = Guid.Empty)));
    }

    [Fact]
    public void Constructor_throws_ArgumentOutOfRangeException_when_the_timeout_is_not_positive()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OrvanoClient(TestClients.Options(options => options.Timeout = TimeSpan.Zero)));
    }

    [Fact]
    [Trait("Spec", "0002:LIB-8")]
    public void ToString_never_shows_the_key_when_called_on_the_client_or_its_options()
    {
        OrvanoClientOptions options = TestClients.Options();

        options.ToString().ShouldNotContain(TestClients.Key);
        new OrvanoClient(options).ToString().ShouldNotContain(TestClients.Key);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-4")]
    public void AddOrvano_registers_a_client_when_configured()
    {
        var services = new ServiceCollection();
        services.AddOrvano(options =>
        {
            options.Endpoint = new Uri("https://orvano.example.test");
            options.ProjectId = TestClients.ProjectId;
            options.ApiKey = TestClients.Key;
        });

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<OrvanoClient>().Health.ShouldNotBeNull();
    }

    [Fact]
    [Trait("AC", "0014:AC-18")]
    public async Task Health_GetAsync_returns_the_body_when_the_server_answers_200()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.OK, TestClients.HealthBody);

        HealthResponse health = await TestClients.Client(handler).Health.GetAsync(cancellationToken: TestContext.Current.CancellationToken);

        health.Status.ShouldBe("ok");
        health.ApiVersions.ShouldBe(["2026-10-22"]);
    }
}
