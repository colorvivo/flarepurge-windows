using System.Linq;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;
using FlarePurge.Core.Services;
using FlarePurge.Tests.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Services;

public class ZoneServiceTests
{
    private static Zone Z(string id, string name) =>
        new(id, name, "active", null, null, null, null, null);

    [Fact]
    public async Task FetchZonesAsync_SendsOrderedQueryAndAccountFilter()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<Zone[]>(true, [], [], [Z("z1", "a.com")], null));
        var svc = new ZoneService(client);

        await svc.FetchZonesAsync("acc-42", page: 3, perPage: 25);

        var query = client.Calls.Single().Query!;
        query.Should().Contain(("page", "3"));
        query.Should().Contain(("per_page", "25"));
        query.Should().Contain(("order", "name"));
        query.Should().Contain(("direction", "asc"));
        query.Should().Contain(("status", "active"));
        query.Should().Contain(("account.id", "acc-42"));
    }

    [Fact]
    public async Task FetchZonesAsync_OmitsAccountIdWhenNull()
    {
        var client = new FakeApiClient().EnqueueGet(
            new ApiResponse<Zone[]>(true, [], [], [], null));
        var svc = new ZoneService(client);

        await svc.FetchZonesAsync(accountId: null, page: 1, perPage: 50);

        client.Calls.Single().Query!.Should().NotContain(tuple => tuple.Key == "account.id");
    }

    [Fact]
    public async Task FetchAllZonesAsync_FollowsPaginationUntilNoMore()
    {
        var info1 = new ResultInfo(Page: 1, PerPage: 50, TotalPages: 3, Count: 50, TotalCount: 125);
        var info2 = new ResultInfo(Page: 2, PerPage: 50, TotalPages: 3, Count: 50, TotalCount: 125);
        var info3 = new ResultInfo(Page: 3, PerPage: 50, TotalPages: 3, Count: 25, TotalCount: 125);
        var client = new FakeApiClient()
            .EnqueueGet(new ApiResponse<Zone[]>(true, [], [], [Z("z1", "a.com")], info1))
            .EnqueueGet(new ApiResponse<Zone[]>(true, [], [], [Z("z2", "b.com"), Z("z3", "c.com")], info2))
            .EnqueueGet(new ApiResponse<Zone[]>(true, [], [], [Z("z4", "d.com")], info3));
        var svc = new ZoneService(client);

        var zones = await svc.FetchAllZonesAsync();

        zones.Should().HaveCount(4);
        client.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task FetchAllZonesAsync_StopsOnNullResultInfo()
    {
        var client = new FakeApiClient()
            .EnqueueGet(new ApiResponse<Zone[]>(true, [], [], [Z("z", "a.com")], null));
        var svc = new ZoneService(client);

        var zones = await svc.FetchAllZonesAsync();

        zones.Should().ContainSingle();
        client.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchAllZonesAsync_ExceedingDepthCap_ThrowsUnknown()
    {
        var client = new FakeApiClient();
        // Seed MaxPaginationDepth+1 responses, each claiming "still more".
        for (var i = 1; i <= ZoneService.MaxPaginationDepth + 1; i++)
        {
            client.EnqueueGet(new ApiResponse<Zone[]>(
                true, [], [],
                [Z($"z{i}", $"{i}.com")],
                new ResultInfo(Page: i, PerPage: 1, TotalPages: 9_999, Count: 1, TotalCount: 9_999)));
        }
        var svc = new ZoneService(client);

        var act = async () => await svc.FetchAllZonesAsync();

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unknown>()
            .Which.Detail.Should().Contain(ZoneService.MaxPaginationDepth.ToString());
    }
}
