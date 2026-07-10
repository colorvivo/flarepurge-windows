using FlarePurge.Core.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class EndpointsTests
{
    [Fact]
    public void Base_EndsWithSlash_SoRelativePathsAppend()
    {
        Endpoints.Base.ToString().Should().Be("https://api.cloudflare.com/client/v4/");
    }

    [Fact]
    public void BuildUri_Zones_NoQuery_ProducesAbsoluteUri()
    {
        var uri = Endpoints.BuildUri(Endpoints.Zones);

        uri.ToString().Should().Be("https://api.cloudflare.com/client/v4/zones");
    }

    [Fact]
    public void BuildUri_PurgeCache_InterpolatesZoneId()
    {
        var uri = Endpoints.BuildUri(Endpoints.PurgeCache("abc-123"));

        uri.AbsolutePath.Should().EndWith("/zones/abc-123/purge_cache");
    }

    [Fact]
    public void BuildUri_WithQuery_EncodesKeyAndValue()
    {
        var uri = Endpoints.BuildUri(
            Endpoints.Zones,
            [("page", "1"), ("per_page", "50"), ("order", "name")]);

        uri.Query.Should().Be("?page=1&per_page=50&order=name");
    }

    [Fact]
    public void BuildUri_WithQueryNeedingEncoding_PercentEncodes()
    {
        var uri = Endpoints.BuildUri(
            Endpoints.Zones,
            [("name", "example.com/path"), ("tag", "a&b")]);

        uri.Query.Should().Be("?name=example.com%2Fpath&tag=a%26b");
    }

    [Fact]
    public void BuildUri_EmptyQueryList_NoQueryStringAppended()
    {
        var uri = Endpoints.BuildUri(Endpoints.Zones, []);

        uri.Query.Should().BeEmpty();
    }

    [Fact]
    public void BuildUri_PathWithLeadingSlash_TrimmedCorrectly()
    {
        var uri = Endpoints.BuildUri("/user/tokens/verify");

        uri.AbsolutePath.Should().Be("/client/v4/user/tokens/verify");
    }

    // --- N2: zone id must be URL-path safe before interpolation ---

    [Theory]
    [InlineData("023e105f4ecef8ad9ca31a8372d0c353")] // real 32-hex id
    [InlineData("abc-123")]                            // test placeholder
    [InlineData("zone_a")]
    public void PurgeCache_PathSafeId_BuildsPath(string zoneId)
    {
        Endpoints.PurgeCache(zoneId).Should().Be($"/zones/{zoneId}/purge_cache");
    }

    [Theory]
    [InlineData("../admin")]
    [InlineData("zone/../other")]
    [InlineData("zone/purge")]
    [InlineData("zone id")]
    [InlineData("zone%2Fx")]
    [InlineData("")]
    public void PurgeCache_PathHostileId_Throws(string zoneId)
    {
        var act = () => Endpoints.PurgeCache(zoneId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("abc123", true)]
    [InlineData("a-b_c", true)]
    [InlineData("a/b", false)]
    [InlineData("a.b", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsPathSafeId_ClassifiesCharset(string? id, bool expected)
    {
        Endpoints.IsPathSafeId(id).Should().Be(expected);
    }
}
