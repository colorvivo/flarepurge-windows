using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;
using FlarePurge.Core.Services;

namespace FlarePurge.App.Demo;

internal sealed class DemoZoneService : IZoneService
{
    public async Task<IReadOnlyList<Zone>> FetchAllZonesAsync(
        string? accountId = null,
        CancellationToken ct = default)
    {
        await Task.Delay(240, ct).ConfigureAwait(false);
        var zones = DemoData.Zones;
        if (!string.IsNullOrWhiteSpace(accountId))
            zones = [.. zones.Where(z => z.AccountId == accountId)];
        return zones;
    }

    public async Task<(IReadOnlyList<Zone> Zones, ResultInfo? Info)> FetchZonesAsync(
        string? accountId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        var all = await FetchAllZonesAsync(accountId, ct).ConfigureAwait(false);
        return (all, new ResultInfo(1, all.Count, 1, all.Count, all.Count));
    }
}
