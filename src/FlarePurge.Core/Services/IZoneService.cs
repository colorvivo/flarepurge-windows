using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Services;

public interface IZoneService
{
    Task<IReadOnlyList<Zone>> FetchAllZonesAsync(
        string? accountId = null,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Zone> Zones, ResultInfo? Info)> FetchZonesAsync(
        string? accountId,
        int page,
        int perPage,
        CancellationToken ct = default);
}
