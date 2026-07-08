using System.Linq;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;
using FlarePurge.Core.Services;
using FlarePurge.Tests.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Services;

public class CacheServiceTests
{
    private static ApiResponse<CachePurgeResult> Ok(string id) =>
        new(Success: true, Errors: [], Messages: [], Result: new CachePurgeResult(id), ResultInfo: null);

    [Fact]
    public async Task PurgeEverythingAsync_PostsEverythingRequest()
    {
        var client = new FakeApiClient().EnqueuePost(Ok("p1"));
        var svc = new CacheService(client);

        var result = await svc.PurgeEverythingAsync("zone-a");

        result.Id.Should().Be("p1");
        var call = client.Calls.Should().ContainSingle().Subject;
        call.Path.Should().EndWith("/zones/zone-a/purge_cache");
        call.Body.Should().BeOfType<CachePurgeRequest>()
            .Which.PurgeEverything.Should().BeTrue();
    }

    [Fact]
    public async Task PurgeEverythingAsync_NullResult_ThrowsDecoding()
    {
        var client = new FakeApiClient().EnqueuePost(
            new ApiResponse<CachePurgeResult>(true, [], [], null, null));
        var svc = new CacheService(client);

        var act = async () => await svc.PurgeEverythingAsync("z");

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Decoding>();
    }

    [Fact]
    public async Task PurgeHostsAsync_PostsFromHostsRequest()
    {
        var client = new FakeApiClient().EnqueuePost(Ok("p"));
        var svc = new CacheService(client);

        await svc.PurgeHostsAsync("zone-a", ["cdn.example.com", "api.example.com"]);

        var body = client.Calls.Single().Body.Should().BeOfType<CachePurgeRequest>().Subject;
        body.Hosts.Should().BeEquivalentTo(new[] { "cdn.example.com", "api.example.com" });
        body.PurgeEverything.Should().BeNull();
        body.Files.Should().BeNull();
    }

    [Fact]
    public async Task PurgeHostsAsync_EmptyList_ThrowsUnknown()
    {
        var svc = new CacheService(new FakeApiClient());

        var act = async () => await svc.PurgeHostsAsync("z", []);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unknown>();
    }

    [Fact]
    public async Task PurgeUrlsAsync_ChunksAtThirty()
    {
        var client = new FakeApiClient();
        for (var i = 0; i < 3; i++)
            client.EnqueuePost(Ok($"p{i}"));
        var svc = new CacheService(client);

        var urls = Enumerable.Range(1, 65).Select(i => $"https://example.com/{i}").ToArray();
        var result = await svc.PurgeUrlsAsync("zone", urls);

        result.Chunks.Should().HaveCount(3);
        result.Chunks[0].UrlCount.Should().Be(30);
        result.Chunks[1].UrlCount.Should().Be(30);
        result.Chunks[2].UrlCount.Should().Be(5);
        result.IsFullSuccess.Should().BeTrue();
        result.FirstPurgeId.Should().Be("p0");
        client.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task PurgeUrlsAsync_ChunkFailure_CapturedWithoutAbortingBatch()
    {
        var client = new FakeApiClient()
            .EnqueuePost(Ok("p0"))
            .EnqueueFailure(new CloudflareApiError.ServerError(500, null, "db"))
            .EnqueuePost(Ok("p2"));
        var svc = new CacheService(client);

        var urls = Enumerable.Range(1, 70).Select(i => $"https://example.com/{i}").ToArray();
        var result = await svc.PurgeUrlsAsync("zone", urls);

        result.Chunks.Should().HaveCount(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.IsFullSuccess.Should().BeFalse();
        result.FirstFailure.Should().BeOfType<CloudflareApiError.ServerError>();
        result.Chunks[1].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeUrlsAsync_EmptyList_ThrowsUnknown()
    {
        var svc = new CacheService(new FakeApiClient());

        var act = async () => await svc.PurgeUrlsAsync("z", []);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unknown>();
    }

    [Fact]
    public async Task PurgeUrlsAsync_SingleChunkBelowMax_SendsOneCall()
    {
        var client = new FakeApiClient().EnqueuePost(Ok("p"));
        var svc = new CacheService(client);

        var result = await svc.PurgeUrlsAsync("z", ["https://a.com/1", "https://a.com/2"]);

        result.Chunks.Should().ContainSingle().Which.UrlCount.Should().Be(2);
        client.Calls.Should().HaveCount(1);
    }
}
