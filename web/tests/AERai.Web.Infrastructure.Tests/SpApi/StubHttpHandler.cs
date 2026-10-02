using System.Net;

namespace AERai.Web.Infrastructure.Tests.SpApi;

/// <summary>Records requests and replies from a scripted queue of responses.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> AccessTokensSeen { get; } = [];

    /// <summary>Request bodies, captured at send time (callers dispose requests afterwards).</summary>
    public List<string?> Bodies { get; } = [];

    public StubHttpHandler Reply(HttpStatusCode status, string body = "{}")
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        return this;
    }

    public StubHttpHandler Reply(Func<HttpRequestMessage, HttpResponseMessage> reply)
    {
        _responses.Enqueue(reply);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        AccessTokensSeen.Add(request.Headers.TryGetValues("x-amz-access-token", out var values) ? values.Single() : null);
        return _responses.Dequeue()(request);
    }
}

/// <summary><see cref="IHttpClientFactory"/> handing out clients over stub handlers by name.</summary>
internal sealed class StubHttpClientFactory(Dictionary<string, HttpMessageHandler> handlers) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handlers[name], disposeHandler: false);
}
