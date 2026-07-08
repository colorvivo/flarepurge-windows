using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlarePurge.Tests.Api;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responders = new();

    public List<HttpRequestMessage> Received { get; } = new();
    public List<string?> ReceivedBodies { get; } = new();

    public FakeHttpMessageHandler EnqueueJson(HttpStatusCode status, string jsonBody, Action<HttpResponseMessage>? modify = null)
    {
        _responders.Enqueue((_, _) =>
        {
            var resp = new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
            modify?.Invoke(resp);
            return Task.FromResult(resp);
        });
        return this;
    }

    public FakeHttpMessageHandler EnqueueStatus(HttpStatusCode status, Action<HttpResponseMessage>? modify = null)
    {
        _responders.Enqueue((_, _) =>
        {
            var resp = new HttpResponseMessage(status);
            modify?.Invoke(resp);
            return Task.FromResult(resp);
        });
        return this;
    }

    public FakeHttpMessageHandler EnqueueException(Exception ex)
    {
        _responders.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(ex));
        return this;
    }

    public FakeHttpMessageHandler EnqueueCanceled(CancellationToken token)
    {
        _responders.Enqueue((_, _) => Task.FromCanceled<HttpResponseMessage>(token));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Received.Add(request);
        ReceivedBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        if (_responders.Count == 0)
            throw new InvalidOperationException("No more responses enqueued");
        return await _responders.Dequeue()(request, ct);
    }
}
