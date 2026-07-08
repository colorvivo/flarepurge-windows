using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;

namespace FlarePurge.Tests.Api;

internal sealed class FakeApiClient : IApiClient
{
    private readonly Queue<Func<object>> _responses = new();

    public List<CallRecord> Calls { get; } = new();

    public FakeApiClient EnqueueGet<T>(ApiResponse<T> response)
    {
        _responses.Enqueue(() => response);
        return this;
    }

    public FakeApiClient EnqueuePost<T>(ApiResponse<T> response)
    {
        _responses.Enqueue(() => response);
        return this;
    }

    public FakeApiClient EnqueueFailure(CloudflareApiError error)
    {
        _responses.Enqueue(() => throw new CloudflareApiException(error));
        return this;
    }

    public Task<ApiResponse<T>> GetAsync<T>(
        string path,
        IReadOnlyList<(string Key, string Value)>? query = null,
        CancellationToken ct = default)
    {
        Calls.Add(new CallRecord(HttpMethod.Get, path, query, Body: null));
        return Dispatch<T>();
    }

    public Task<ApiResponse<T>> PostAsync<TBody, T>(string path, TBody body, CancellationToken ct = default)
    {
        Calls.Add(new CallRecord(HttpMethod.Post, path, Query: null, Body: body));
        return Dispatch<T>();
    }

    private Task<ApiResponse<T>> Dispatch<T>()
    {
        if (_responses.Count == 0)
            throw new InvalidOperationException("No queued response.");
        var next = _responses.Dequeue();
        try
        {
            return Task.FromResult((ApiResponse<T>)next());
        }
        catch (CloudflareApiException ex)
        {
            return Task.FromException<ApiResponse<T>>(ex);
        }
    }

    internal sealed record CallRecord(
        HttpMethod Method,
        string Path,
        IReadOnlyList<(string Key, string Value)>? Query,
        object? Body);
}
