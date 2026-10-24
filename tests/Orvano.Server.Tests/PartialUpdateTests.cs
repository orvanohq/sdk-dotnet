using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orvano.Server.Tests;

public sealed record PatchSample
{
    [JsonPropertyName("name")]
    [JsonConverter(typeof(OptionalJsonConverter<string>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string> Name { get; init; }

    [JsonPropertyName("nickname")]
    [JsonConverter(typeof(OptionalJsonConverter<string?>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string?> Nickname { get; init; }

    [JsonPropertyName("rank")]
    [JsonConverter(typeof(OptionalJsonConverter<int?>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int?> Rank { get; init; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PatchSample))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int?))]
internal sealed partial class PatchJsonContext : JsonSerializerContext;

// Spec 0014 JSW-10, CNC-4: partial update fields on the wire, and If-Match.
public sealed class PartialUpdateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Spec", "0014:JSW-10")]
    public void Optional_field_is_left_out_while_unset_and_sent_when_set_even_to_null()
    {
        string json = JsonSerializer.Serialize(new PatchSample { Nickname = null, Rank = 3 }, PatchJsonContext.Default.PatchSample);

        json.ShouldBe("""{"nickname":null,"rank":3}""");
        JsonSerializer.Serialize(new PatchSample(), PatchJsonContext.Default.PatchSample).ShouldBe("{}");
    }

    [Fact]
    [Trait("Spec", "0014:JSW-10")]
    public void Optional_field_reads_back_unset_null_and_set()
    {
        PatchSample sample = JsonSerializer.Deserialize("""{"nickname":null,"name":"a"}""", PatchJsonContext.Default.PatchSample)!;

        sample.Name.ShouldBe(new Optional<string>("a"));
        sample.Nickname.HasValue.ShouldBeTrue();
        sample.Nickname.Value.ShouldBeNull();
        sample.Rank.HasValue.ShouldBeFalse();
        sample.Rank.ShouldBe(default);
    }

    [Fact]
    [Trait("Spec", "0014:CNC-4")]
    public async Task Request_sends_the_version_as_a_strong_if_match_when_given()
    {
        StubHandler handler = new StubHandler().Answer(HttpStatusCode.OK, TestClients.HealthBody);

        await TestClients.Client(handler).Health.GetAsync(OrvanoRequestOptions.WithIfMatch(null, "AAAAAAAAAAE"), Token);

        handler.Requests.ShouldHaveSingleItem().Headers.GetValues(OrvanoHeaders.IfMatch).ShouldBe(["\"AAAAAAAAAAE\""]);
    }

    [Fact]
    [Trait("Spec", "0014:CNC-4")]
    public void WithIfMatch_keeps_the_other_options_and_refuses_a_quoted_value()
    {
        var options = new OrvanoRequestOptions { Timeout = TimeSpan.FromSeconds(3) };

        OrvanoRequestOptions.WithIfMatch(options, "AAAAAAAAAAE")!.Timeout.ShouldBe(TimeSpan.FromSeconds(3));
        OrvanoRequestOptions.WithIfMatch(options, null).ShouldBeSameAs(options);
        Should.Throw<ArgumentException>(() => OrvanoRequestOptions.WithIfMatch(null, "\"AAAAAAAAAAE\""));
        Should.Throw<ArgumentException>(() => OrvanoRequestOptions.WithIfMatch(null, ""));
    }

    [Fact]
    [Trait("Spec", "0014:CNC-4")]
    public async Task Headers_refuse_if_match_which_the_ifMatch_parameter_sets()
    {
        var options = new OrvanoRequestOptions { Headers = new Dictionary<string, string> { [OrvanoHeaders.IfMatch] = "\"x\"" } };

        await Should.ThrowAsync<ArgumentException>(() => TestClients.Client(new StubHandler()).Health.GetAsync(options, Token));
    }
}
