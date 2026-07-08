using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Models;
using FlarePurge.Core.Services;

namespace FlarePurge.App.Demo;

internal sealed class DemoCacheService : ICacheService
{
    public async Task<CachePurgeResult> PurgeEverythingAsync(string zoneId, CancellationToken ct = default)
    {
        await Task.Delay(650, ct).ConfigureAwait(false);
        return new CachePurgeResult(NewId("everything", zoneId));
    }

    public async Task<CachePurgeBatchResult> PurgeUrlsAsync(string zoneId, IReadOnlyList<string> urls, CancellationToken ct = default)
    {
        await Task.Delay(420, ct).ConfigureAwait(false);
        var chunks = new List<CachePurgeBatchResult.ChunkOutcome>();
        for (var i = 0; i < Math.Max(1, (urls.Count + 29) / 30); i++)
        {
            var size = Math.Min(30, urls.Count - (i * 30));
            chunks.Add(new CachePurgeBatchResult.ChunkOutcome(i, size, NewId("urls", zoneId + i), null));
        }
        return new CachePurgeBatchResult(chunks);
    }

    public async Task<CachePurgeResult> PurgeHostsAsync(string zoneId, IReadOnlyList<string> hosts, CancellationToken ct = default)
    {
        await Task.Delay(420, ct).ConfigureAwait(false);
        return new CachePurgeResult(NewId("hosts", zoneId));
    }

    private static string NewId(string kind, string salt) =>
        $"demo-{kind}-{Math.Abs(salt.GetHashCode()):x8}-{DateTime.UtcNow:HHmmssfff}";
}
