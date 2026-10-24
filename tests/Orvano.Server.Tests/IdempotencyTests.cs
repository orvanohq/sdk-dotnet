using System.Net;

namespace Orvano.Server.Tests;

// Spec 0014 IDM-1, IDM-3: the Idempotency-Key a marked operation's idempotencyKey parameter sends, and the retry rule.
public sealed class IdempotencyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly OrvanoCall Post = new("Test_Post", HttpMethod.Post, ["v1", "test"]);

    [Fact]
    [Trait("Spec", "0014:IDM-3")]
    public async Task Post_is_retried_with_the_same_key_when_it_has_an_idempotency_key()
    {
        StubHandler handler = new StubHandler()
            .Answer(HttpStatusCode.ServiceUnavailable, TestClients.Problem(503, "unavailable"), "application/problem+json")
            .Answer(HttpStatusCode.NoContent);

        await TestClients.Client(handler).Transport.SendAsync(Post, OrvanoRequestOptions.WithIdempotencyKey(null, "order-7"), Token);

        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldAllBe(request => request.Headers.GetValues(OrvanoHeaders.IdempotencyKey).Single() == "order-7");
    }

    [Fact]
    [Trait("Spec", "0014:IDM-3")]
    public async Task Post_is_not_retried_when_it_has_no_idempotency_key()
    {
        StubHandler handler = new StubHandler()
            .Answer(HttpStatusCode.ServiceUnavailable, TestClients.Problem(503, "unavailable"), "application/problem+json")
            .Answer(HttpStatusCode.NoContent);

        await Should.ThrowAsync<OrvanoException>(() => TestClients.Client(handler).Transport.SendAsync(Post, null, Token));

        handler.Requests.ShouldHaveSingleItem().Headers.Contains(OrvanoHeaders.IdempotencyKey).ShouldBeFalse();
    }

    [Fact]
    [Trait("Spec", "0014:IDM-1")]
    public void WithIdempotencyKey_refuses_a_key_that_is_not_1_to_255_printable_ascii()
    {
        Should.Throw<ArgumentException>(() => OrvanoRequestOptions.WithIdempotencyKey(null, ""));
        Should.Throw<ArgumentException>(() => OrvanoRequestOptions.WithIdempotencyKey(null, "café"));
        Should.Throw<ArgumentException>(() => OrvanoRequestOptions.WithIdempotencyKey(null, new string('k', 256)));
        OrvanoRequestOptions.WithIdempotencyKey(null, new string('k', 255))!.IdempotencyKey.ShouldBe(new string('k', 255));
    }

    [Fact]
    [Trait("Spec", "0014:IDM-1")]
    public void WithIdempotencyKey_and_WithIfMatch_keep_each_other()
    {
        OrvanoRequestOptions both = OrvanoRequestOptions.WithIfMatch(OrvanoRequestOptions.WithIdempotencyKey(null, "k"), "AAAAAAAAAAE")!;

        both.IdempotencyKey.ShouldBe("k");
        both.IfMatch.ShouldBe("AAAAAAAAAAE");
        OrvanoRequestOptions.WithIdempotencyKey(both, "j")!.IfMatch.ShouldBe("AAAAAAAAAAE");
    }

    [Fact]
    [Trait("Spec", "0014:IDM-1")]
    public async Task Headers_refuse_idempotency_key_which_the_idempotencyKey_parameter_sets()
    {
        var options = new OrvanoRequestOptions { Headers = new Dictionary<string, string> { [OrvanoHeaders.IdempotencyKey] = "k" } };

        await Should.ThrowAsync<ArgumentException>(() => TestClients.Client(new StubHandler()).Health.GetAsync(options, Token));
    }
}
