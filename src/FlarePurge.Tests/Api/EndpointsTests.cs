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
}
