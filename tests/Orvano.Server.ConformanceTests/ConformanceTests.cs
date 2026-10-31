// Spec 0014 AC-17, AC-18, CNF-5, spec 0002 LIB-12: every server scenario against Orvano and the stub from
// api/conformance/compose.yaml at the pinned orvano commit (see README.md).
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

[assembly: AssemblyFixture(typeof(Orvano.Server.ConformanceTests.ConformanceRun))]

namespace Orvano.Server.ConformanceTests;

/// <summary>
/// The scenarios, the skips, the project they share (CNF-7), and which scenarios ran; CNF-8 is checked when the run ends.
/// </summary>
public sealed class ConformanceRun : IAsyncLifetime
{
    internal static IReadOnlyList<JsonObject> Scenarios { get; } =
        [.. (Read("Generated/scenarios.json")?["scenarios"]?.AsArray() ?? []).OfType<JsonObject>()];

    internal static IReadOnlyList<JsonObject> Skips { get; } = [.. (Read("skip.json")?.AsArray() ?? []).OfType<JsonObject>()];

    internal ConcurrentDictionary<string, bool> Ran { get; } = new(StringComparer.Ordinal);

    internal BootstrappedProject? Project { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (Scenarios.Any(ConformanceRunner.RequiresProject))
        {
            Project = await ConformanceRunner.BootstrapProjectAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        // CNF-8 is off only when you run a few scenarios with a filter; CI never sets it.
        if (Environment.GetEnvironmentVariable("ORVANO_CONFORMANCE_PARTIAL") == "1")
        {
            return ValueTask.CompletedTask;
        }
        IReadOnlyList<string> problems = ConformanceRunner.NotRun(
            Scenarios.Select(scenario => scenario["id"]?.GetValue<string>() ?? string.Empty),
            Ran.Keys.ToHashSet(StringComparer.Ordinal),
            Skips);
        return problems.Count == 0
            ? ValueTask.CompletedTask
            : throw new InvalidOperationException($"0014 CNF-8: {string.Join(" ", problems)}");
    }

    private static JsonNode? Read(string path) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, path)));
}

public sealed class ConformanceTests(ConformanceRun run)
{
    private static readonly Servers Servers = new(
        new Uri(Environment.GetEnvironmentVariable("ORVANO_CONFORMANCE_STUB_URL") ?? "http://localhost:18081"),
        new Uri(Environment.GetEnvironmentVariable("ORVANO_CONFORMANCE_URL") ?? "http://localhost:18080"));

    public static TheoryData<string> ScenarioIds { get; } =
        [.. ConformanceRun.Scenarios.Select(scenario => scenario["id"]?.GetValue<string>() ?? string.Empty)];

    [Theory]
    [MemberData(nameof(ScenarioIds))]
    [Trait("AC", "0014:AC-17")]
    [Trait("AC", "0014:AC-18")]
    public async Task Scenario_passes_when_run_against_the_pinned_servers(string id)
    {
        if (ConformanceRun.Skips.FirstOrDefault(skip => skip["id"]?.GetValue<string>() == id) is { } skip)
        {
            Assert.Skip($"{skip["reason"]} ({skip["issue"]})");
        }
        run.Ran[id] = true;
        JsonObject scenario = ConformanceRun.Scenarios.First(entry => entry["id"]?.GetValue<string>() == id);

        IReadOnlyList<string> problems = await ConformanceRunner.RunScenarioAsync(
            scenario, Servers, run.Project, TestContext.Current.CancellationToken);

        problems.ShouldBeEmpty();
    }
}
