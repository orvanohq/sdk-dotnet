using System.Net;
using System.Net.Http.Headers;

namespace Orvano.Server.Tests;

public sealed class TransportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Spec", "0014:VRS-3")]
    public async Task Request_carries_the_version_client_and_key_headers_when_sent()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.OK, TestClients.HealthBody);

        await TestClients.Client(handler).Health.GetAsync(cancellationToken: Token);

        HttpRequestMessage request = handler.Requests.ShouldHaveSingleItem();
        request.Headers.GetValues(OrvanoHeaders.OrvanoApiVersion).ShouldBe([OrvanoContract.ApiVersion]);
        request.Headers.GetValues(OrvanoHeaders.OrvanoKey).ShouldBe([TestClients.Key]);
        request.Headers.GetValues(OrvanoHeaders.OrvanoClient).Single().ShouldStartWith("Orvano.Server/");
    }

    [Fact]
    public async Task Request_path_stays_under_the_endpoint_path_when_the_endpoint_has_one()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.OK, TestClients.HealthBody);

        await TestClients.Client(handler, options => options.Endpoint = new Uri("https://example.test/orvano"))
            .Health.GetAsync(cancellationToken: Token);

        handler.Requests.ShouldHaveSingleItem().RequestUri.ShouldBe(new Uri("https://example.test/orvano/v1/health"));
    }

    [Fact]
    [Trait("Spec", "0002:LIB-7")]
    public async Task GetAsync_retries_when_the_server_answers_503()
    {
        StubHandler handler = new StubHandler()
            .Answer(HttpStatusCode.ServiceUnavailable, TestClients.Problem(503, "unavailable"), "application/problem+json")
            .Answer(HttpStatusCode.OK, TestClients.HealthBody);

        HealthResponse health = await TestClients.Client(handler).Health.GetAsync(cancellationToken: Token);

        health.Status.ShouldBe("ok");
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-7")]
    public async Task GetAsync_does_not_retry_when_MaxRetries_is_0()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.ServiceUnavailable);
        var options = new OrvanoRequestOptions { Retry = new OrvanoRetryOptions { MaxRetries = 0 } };

        OrvanoException error = await Should.ThrowAsync<OrvanoException>(() => TestClients.Client(handler).Health.GetAsync(options, Token));

        error.StatusCode.ShouldBe(503);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-7")]
    public async Task GetAsync_fails_at_once_when_Retry_After_is_longer_than_MaxRetryAfter()
    {
        StubHandler handler = new StubHandler().Answer(
            HttpStatusCode.ServiceUnavailable,
            configure: response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5)));

        await Should.ThrowAsync<OrvanoException>(() => TestClients.Client(handler).Health.GetAsync(cancellationToken: Token));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "0014:IDM-3")]
    public async Task SendAsync_does_not_retry_a_POST_when_the_server_answers_503()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.ServiceUnavailable);
        OrvanoClient client = TestClients.Client(handler);
        var call = new OrvanoCall("Test_Post", HttpMethod.Post, ["v1", "test"]) { Body = OrvanoWire.EmptyObject };

        await Should.ThrowAsync<OrvanoException>(() => client.Transport.SendAsync(call, null, Token));

        handler.Requests.Count.ShouldBe(1);
        handler.Bodies.ShouldBe(["{}"]);
    }

    [Theory]
    [InlineData(400, OrvanoErrorKind.Validation)]
    [InlineData(401, OrvanoErrorKind.Unauthenticated)]
    [InlineData(403, OrvanoErrorKind.Forbidden)]
    [InlineData(404, OrvanoErrorKind.NotFound)]
    [InlineData(408, OrvanoErrorKind.Timeout)]
    [InlineData(409, OrvanoErrorKind.Conflict)]
    [InlineData(412, OrvanoErrorKind.Conflict)]
    [InlineData(413, OrvanoErrorKind.Validation)]
    [InlineData(415, OrvanoErrorKind.Validation)]
    [InlineData(418, OrvanoErrorKind.Server)]
    [InlineData(422, OrvanoErrorKind.Validation)]
    [InlineData(428, OrvanoErrorKind.Conflict)]
    [InlineData(429, OrvanoErrorKind.RateLimited)]
    [InlineData(500, OrvanoErrorKind.Server)]
    [InlineData(503, OrvanoErrorKind.Server)]
    [Trait("Spec", "0014:KND-2")]
    public void KindFor_follows_the_status_table_when_given_a_status(int status, OrvanoErrorKind kind)
    {
        OrvanoException.KindFor(status).ShouldBe(kind);
    }

    [Fact]
    [Trait("Spec", "0014:PRB-3")]
    public async Task GetAsync_throws_with_field_errors_when_the_body_is_a_validation_problem()
    {
        string body = TestClients.Problem(400, "validation_failed", ""","errors":[{"field":"name","location":"body","code":"too_short","message":"M"}]""");
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.BadRequest, body, "application/problem+json");

        OrvanoException error = await Should.ThrowAsync<OrvanoException>(() => TestClients.Client(handler).Health.GetAsync(cancellationToken: Token));

        error.Code.ShouldBe(OrvanoErrorCodes.ValidationFailed);
        error.Kind.ShouldBe(OrvanoErrorKind.Validation);
        error.TraceId.ShouldBe("0af7651916cd43dd8448eb211c80319c");
        error.FieldErrors.ShouldBe([new OrvanoFieldError("name", "body", "too_short", "M")]);
        error.RawBody.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "0014:KND-3")]
    public async Task GetAsync_throws_code_unknown_with_at_most_4_KB_of_body_when_the_body_is_not_a_problem()
    {
        string html = $"<html>{new string('x', 5000)}</html>";
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.BadGateway, html, "text/html");
        var options = new OrvanoRequestOptions { Retry = new OrvanoRetryOptions { MaxRetries = 0 } };

        OrvanoException error = await Should.ThrowAsync<OrvanoException>(() => TestClients.Client(handler).Health.GetAsync(options, Token));

        error.Code.ShouldBe(OrvanoErrorCodes.Unknown);
        error.RawBody.Length.ShouldBe(4096);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-6")]
    public async Task GetAsync_throws_OrvanoRateLimitedException_with_the_wait_when_the_server_answers_429()
    {
        StubHandler handler = new StubHandler().Answer(
            HttpStatusCode.TooManyRequests,
            TestClients.Problem(429, "rate_limited"),
            "application/problem+json",
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)));
        var options = new OrvanoRequestOptions { Retry = new OrvanoRetryOptions { MaxRetries = 0 } };

        OrvanoRateLimitedException error = await Should.ThrowAsync<OrvanoRateLimitedException>(() => TestClients.Client(handler).Health.GetAsync(options, Token));

        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    [Trait("Spec", "0002:LIB-6")]
    public async Task GetAsync_throws_TimeoutException_when_the_last_attempt_times_out()
    {
        StubHandler handler = new StubHandler().Hang().Hang();
        var options = new OrvanoRequestOptions { Timeout = TimeSpan.FromMilliseconds(50), Retry = new OrvanoRetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.Zero } };

        await Should.ThrowAsync<TimeoutException>(() => TestClients.Client(handler).Health.GetAsync(options, Token));

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-6")]
    public async Task GetAsync_throws_OperationCanceledException_when_the_caller_cancels()
    {
        StubHandler handler = new StubHandler().Hang();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() => TestClients.Client(handler).Health.GetAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    [Trait("Spec", "0014:PTH-4")]
    public async Task GetAsync_throws_HttpRequestException_without_retrying_when_the_server_redirects()
    {
        StubHandler handler = new StubHandler().Answer(
            HttpStatusCode.Found,
            configure: response => response.Headers.Location = new Uri("https://elsewhere.example.test/"));

        await Should.ThrowAsync<HttpRequestException>(() => TestClients.Client(handler).Health.GetAsync(cancellationToken: Token));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "0002:LIB-7")]
    public async Task GetAsync_retries_when_the_connection_fails()
    {
        StubHandler handler = new StubHandler()
            .Fail(new HttpRequestException(HttpRequestError.ConnectionError, "refused"))
            .Answer(HttpStatusCode.OK, TestClients.HealthBody);

        await TestClients.Client(handler).Health.GetAsync(cancellationToken: Token);

        handler.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("Orvano-Key")]
    [InlineData("authorization")]
    [InlineData("Orvano-Client")]
    [InlineData("User-Agent")]
    public async Task GetAsync_throws_ArgumentException_before_sending_when_a_header_is_the_SDK_s(string name)
    {
        var handler = new StubHandler();
        var options = new OrvanoRequestOptions { Headers = new Dictionary<string, string> { [name] = "x" } };

        await Should.ThrowAsync<ArgumentException>(() => TestClients.Client(handler).Health.GetAsync(options, Token));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_sends_the_call_s_Orvano_Api_Version_when_the_options_set_one()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.OK, TestClients.HealthBody);
        var options = new OrvanoRequestOptions { Headers = new Dictionary<string, string> { [OrvanoHeaders.OrvanoApiVersion] = "1999-01-01" } };

        await TestClients.Client(handler).Health.GetAsync(options, Token);

        handler.Requests.ShouldHaveSingleItem().Headers.GetValues(OrvanoHeaders.OrvanoApiVersion).ShouldBe(["1999-01-01"]);
    }
}
