using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Services;

public sealed class CacheService(IApiClient client) : ICacheService
{
    public const int MaxUrlsPerRequest = 30;

    private readonly IApiClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<CachePurgeResult> PurgeEverythingAsync(string zoneId, CancellationToken ct = default)
    {
        var response = await _client.PostAsync<CachePurgeRequest, CachePurgeResult>(
            Endpoints.PurgeCache(zoneId),
            CachePurgeRequest.Everything,
            ct).ConfigureAwait(false);
        return response.Result
            ?? throw new CloudflareApiException(new CloudflareApiError.Decoding("nil result for purge_cache"));
    }

    public async Task<CachePurgeBatchResult> PurgeUrlsAsync(string zoneId, IReadOnlyList<string> urls, CancellationToken ct = default)
    {
        if (urls is null || urls.Count == 0)
            throw new CloudflareApiException(new CloudflareApiError.Unknown("No URLs provided"));

        var chunks = Chunk(urls, MaxUrlsPerRequest);
        var outcomes = new List<CachePurgeBatchResult.ChunkOutcome>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            try
            {
                var response = await _client.PostAsync<CachePurgeRequest, CachePurgeResult>(
                    Endpoints.PurgeCache(zoneId),
                    CachePurgeRequest.FromFiles(chunk),
                    ct).ConfigureAwait(false);
                outcomes.Add(new CachePurgeBatchResult.ChunkOutcome(i, chunk.Count, response.Result?.Id, null));
            }
            catch (CloudflareApiException ex)
            {
                outcomes.Add(new CachePurgeBatchResult.ChunkOutcome(i, chunk.Count, null, ex.Error));
            }
        }

        return new CachePurgeBatchResult(outcomes);
    }

    public async Task<CachePurgeResult> PurgeHostsAsync(string zoneId, IReadOnlyList<string> hosts, CancellationToken ct = default)
    {
        if (hosts is null || hosts.Count == 0)
            throw new CloudflareApiException(new CloudflareApiError.Unknown("No hosts provided"));

        var response = await _client.PostAsync<CachePurgeRequest, CachePurgeResult>(
            Endpoints.PurgeCache(zoneId),
            CachePurgeRequest.FromHosts(hosts),
            ct).ConfigureAwait(false);
        return response.Result
            ?? throw new CloudflareApiException(new CloudflareApiError.Decoding("nil result for purge_cache"));
    }

    private static IReadOnlyList<IReadOnlyList<string>> Chunk(IReadOnlyList<string> source, int size)
    {
        var chunks = new List<IReadOnlyList<string>>((source.Count + size - 1) / size);
        for (var i = 0; i < source.Count; i += size)
        {
            var take = Math.Min(size, source.Count - i);
            var chunk = new string[take];
            for (var j = 0; j < take; j++) chunk[j] = source[i + j];
            chunks.Add(chunk);
        }
        return chunks;
    }
}
