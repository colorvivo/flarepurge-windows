using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Services;

public interface ICacheService
{
    Task<CachePurgeResult> PurgeEverythingAsync(string zoneId, CancellationToken ct = default);
    Task<CachePurgeBatchResult> PurgeUrlsAsync(string zoneId, IReadOnlyList<string> urls, CancellationToken ct = default);
    Task<CachePurgeResult> PurgeHostsAsync(string zoneId, IReadOnlyList<string> hosts, CancellationToken ct = default);
}
