using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Services;

public sealed class ZoneService(IApiClient client) : IZoneService
{
    public const int MaxPaginationDepth = 200;
    private const int DefaultPerPage = 50;

    private readonly IApiClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<IReadOnlyList<Zone>> FetchAllZonesAsync(string? accountId = null, CancellationToken ct = default)
    {
        var aggregate = new List<Zone>();
        for (var page = 1; ; page++)
        {
            if (page > MaxPaginationDepth)
            {
                throw new CloudflareApiException(
                    new CloudflareApiError.Unknown(
                        $"Zone pagination exceeded {MaxPaginationDepth} pages; refusing to continue."));
            }

            var (zones, info) = await FetchZonesAsync(accountId, page, DefaultPerPage, ct).ConfigureAwait(false);
            aggregate.AddRange(zones);
            if (info is null || !info.HasMorePages) return aggregate;
        }
    }

    public async Task<(IReadOnlyList<Zone> Zones, ResultInfo? Info)> FetchZonesAsync(
        string? accountId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        var query = new List<(string Key, string Value)>
        {
            ("page", page.ToString()),
            ("per_page", perPage.ToString()),
            ("order", "name"),
            ("direction", "asc"),
            ("status", "active"),
        };
        if (!string.IsNullOrWhiteSpace(accountId))
            query.Add(("account.id", accountId));

        var response = await _client.GetAsync<Zone[]>(Endpoints.Zones, query, ct).ConfigureAwait(false);
        return (response.Result ?? [], response.ResultInfo);
    }
}
