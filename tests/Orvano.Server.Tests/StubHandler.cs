using System.Net;
using System.Text;

namespace Orvano.Server.Tests;

/// <summary>A hand written handler that answers from a script and records what it was sent (spec 0002 LIB-12).</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _answers = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> Bodies { get; } = [];

    public StubHandler Answer(HttpStatusCode status, string? body = null, string contentType = "application/json", Action<HttpResponseMessage>? configure = null)
    {
        _answers.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = new StringContent(body, Encoding.UTF8, contentType);
            }
            configure?.Invoke(response);
            return Task.FromResult(response);
        });
        return this;
    }

    /// <summary>An answer that never comes, until the request is cancelled.</summary>
    public StubHandler Hang()
    {
        _answers.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        return this;
    }

    public StubHandler Fail(HttpRequestException exception)
    {
        _answers.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(exception));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        if (!_answers.TryDequeue(out Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? answer))
        {
            throw new InvalidOperationException($"No answer scripted for request {Requests.Count}.");
        }
        return await answer(request, cancellationToken);
    }
}
