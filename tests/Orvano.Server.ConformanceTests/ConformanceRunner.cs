// Spec 0014 CNF-4, CNF-8: the hand written runner around the generated dispatcher. It opens the client, loads a stub
// scenario's script, runs each step, and matches results, errors, the last response, and what the SDK sent.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Orvano.Server.ConformanceTests;

/// <summary>Where the servers are (api/conformance/compose.yaml).</summary>
internal sealed record Servers(Uri StubUrl, Uri LiveUrl);

internal static partial class ConformanceRunner
{
    private const string ProjectId = "0192f0c1-0000-7000-8000-000000000001";
    private const string DefaultKey = "conformance-key";

    /// <summary>Runs one scenario and returns every mismatch; an empty list is a pass.</summary>
    public static async Task<IReadOnlyList<string>> RunScenarioAsync(JsonObject scenario, Servers servers, CancellationToken cancellationToken)
    {
        string id = scenario["id"]?.GetValue<string>() ?? throw new ArgumentException("A scenario has no id.", nameof(scenario));
        bool stub = scenario["mode"]?.GetValue<string>() == "stub";
        string token = $"run-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}";
        string key = scenario["client"]?["key"]?.GetValue<string>() ?? DefaultKey;
        var variables = new Dictionary<string, string>
        {
            ["apiVersion"] = OrvanoContract.ApiVersion,
            ["clientName"] = OrvanoTransport.ClientName,
            ["key"] = key,
            ["projectId"] = ProjectId,
        };

        using var plain = new HttpClient();
        // The SDK's own client never follows a redirect (OrvanoClient's remarks), so neither does the runner's handler.
        var recording = new RecordingHandler(new SocketsHttpHandler { AllowAutoRedirect = false });
        using var sdkHttp = new HttpClient(recording) { Timeout = Timeout.InfiniteTimeSpan };
        var client = new OrvanoClient(
            new OrvanoClientOptions
            {
                Endpoint = stub ? servers.StubUrl : servers.LiveUrl,
                ProjectId = Guid.Parse(ProjectId),
                ApiKey = key,
            },
            sdkHttp);

        if (stub)
        {
            var script = new JsonObject { ["stub"] = ConformanceMatch.Substitute(scenario["stub"], variables) };
            using var content = new StringContent(script.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage loaded = await plain
                .PostAsync(new Uri(servers.StubUrl, $"/__stub/runs/{token}/scenarios/{id}"), content, cancellationToken);
            if ((int)loaded.StatusCode != 204)
            {
                return [$"the stub refused the scenario ({(int)loaded.StatusCode})"];
            }
        }

        var problems = new List<string>();
        Dictionary<string, string> runHeaders = stub
            ? new Dictionary<string, string> { ["Orvano-Stub-Run"] = token }
            : new Dictionary<string, string>();
        JsonArray steps = scenario["steps"]?.AsArray() ?? [];
        for (int index = 0; index < steps.Count; index++)
        {
            JsonObject step = steps[index]?.AsObject() ?? [];
            string where = $"step {index + 1}";
            var options = ConformanceOptions.FromJson(ConformanceMatch.Substitute(step["options"], variables) as JsonObject, runHeaders);
            JsonObject parameters = ConformanceMatch.Substitute(step["params"], variables) as JsonObject ?? [];
            string call = step["call"]?.GetValue<string>() ?? string.Empty;
            ConformanceOutcome outcome = await ConformanceDispatcher.DispatchAsync(client, call, parameters, options, cancellationToken);
            JsonObject expected = ConformanceMatch.Substitute(step["expect"], variables) as JsonObject ?? [];
            if (expected.ContainsKey("result"))
            {
                problems.AddRange(outcome.IsOk
                    ? ConformanceMatch.Match(expected["result"], outcome.Result, $"{where} result")
                    : [$"{where}: failed with {outcome.Error?.ToJsonString()}"]);
            }
            else
            {
                problems.AddRange(outcome.IsOk
                    ? [$"{where}: succeeded, expected an error"]
                    : ConformanceMatch.Match(expected["error"], outcome.Error, $"{where} error"));
            }
            if (expected["response"] is { } response)
            {
                problems.AddRange(ConformanceMatch.Match(response, recording.Last?.ToJson(), $"{where} response"));
            }
        }

        if (stub)
        {
            var recorded = JsonNode.Parse(
                await plain.GetStringAsync(new Uri(servers.StubUrl, $"/__stub/runs/{token}/requests"), cancellationToken));
            problems.AddRange(ConformanceMatch.Match(
                ConformanceMatch.Substitute(scenario["expectRequests"], variables),
                recorded?["requests"],
                "requests"));
        }
        return problems;
    }

    /// <summary>CNF-8: every scenario either ran or is in skip.json with a reason and an issue.</summary>
    public static IReadOnlyList<string> NotRun(IEnumerable<string> scenarioIds, IReadOnlySet<string> ran, IReadOnlyList<JsonObject> skips)
    {
        var problems = skips
            .Where(skip => string.IsNullOrEmpty(skip["reason"]?.GetValue<string>())
                || !Issue().IsMatch(skip["issue"]?.GetValue<string>() ?? string.Empty))
            .Select(skip => $"skip.json: {skip["id"]} needs a reason and an orvanohq issue link.")
            .ToList();
        var skipped = skips.Select(skip => skip["id"]?.GetValue<string>()).ToHashSet();
        problems.AddRange(scenarioIds
            .Where(id => !ran.Contains(id) && !skipped.Contains(id))
            .Select(id => $"{id} did not run and is not in skip.json."));
        return problems;
    }

    [GeneratedRegex(@"^https://github\.com/orvanohq/[^/]+/issues/\d+$")]
    private static partial Regex Issue();
}
