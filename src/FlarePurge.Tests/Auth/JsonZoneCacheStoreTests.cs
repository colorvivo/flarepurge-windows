using System;
using System.IO;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class JsonZoneCacheStoreTests : IDisposable
{
    private readonly string _path;

    public JsonZoneCacheStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"flarepurge-zones-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        var tmp = _path + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    private static Zone SampleZone(string id = "z1", string name = "example.com")
        => new(id, name, "active", new[] { "ns1.cloudflare.com", "ns2.cloudflare.com" },
            "cf-acc-1", "Color Vivo", "Free Website", new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Get_Missing_ReturnsNull()
    {
        var store = new JsonZoneCacheStore(_path);
        store.Get("nope").Should().BeNull();
    }

    [Fact]
    public void Save_ThenGet_RoundTrips()
    {
        var store = new JsonZoneCacheStore(_path);
        var zones = new[] { SampleZone(), SampleZone("z2", "example.net") };

        store.Save("local-1", zones);

        var reloaded = new JsonZoneCacheStore(_path).Get("local-1");
        reloaded.Should().NotBeNull();
        reloaded!.AccountId.Should().Be("local-1");
        reloaded.Zones.Should().HaveCount(2);
        reloaded.Zones[0].Id.Should().Be("z1");
        reloaded.Zones[0].Name.Should().Be("example.com");
        reloaded.Zones[0].Status.Should().Be("active");
        reloaded.Zones[0].AccountId.Should().Be("cf-acc-1");
        reloaded.Zones[0].AccountName.Should().Be("Color Vivo");
        reloaded.Zones[0].PlanName.Should().Be("Free Website");
        reloaded.Zones[0].NameServers.Should().Equal("ns1.cloudflare.com", "ns2.cloudflare.com");
        reloaded.FetchedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Save_OverwritesPreviousEntryForSameAccount()
    {
        var store = new JsonZoneCacheStore(_path);
        store.Save("local-1", new[] { SampleZone("z1") });
        store.Save("local-1", new[] { SampleZone("z1"), SampleZone("z2", "two.com") });

        store.Get("local-1")!.Zones.Should().HaveCount(2);
    }

    [Fact]
    public void Save_KeepsOtherAccountsIntact()
    {
        var store = new JsonZoneCacheStore(_path);
        store.Save("local-1", new[] { SampleZone("z1") });
        store.Save("local-2", new[] { SampleZone("z9", "other.com") });

        store.Get("local-1")!.Zones[0].Id.Should().Be("z1");
        store.Get("local-2")!.Zones[0].Id.Should().Be("z9");
    }

    [Fact]
    public void Delete_RemovesOnlyTargetedAccount()
    {
        var store = new JsonZoneCacheStore(_path);
        store.Save("local-1", new[] { SampleZone("z1") });
        store.Save("local-2", new[] { SampleZone("z9", "other.com") });

        store.Delete("local-1");

        store.Get("local-1").Should().BeNull();
        store.Get("local-2").Should().NotBeNull();
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var store = new JsonZoneCacheStore(_path);
        store.Save("local-1", new[] { SampleZone() });
        store.Save("local-2", new[] { SampleZone("z9", "other.com") });

        store.Clear();

        store.Get("local-1").Should().BeNull();
        store.Get("local-2").Should().BeNull();
        File.Exists(_path).Should().BeFalse();
    }

    [Fact]
    public void CorruptFile_TreatedAsEmpty()
    {
        File.WriteAllText(_path, "{ bad json");
        var store = new JsonZoneCacheStore(_path);

        store.Get("local-1").Should().BeNull();
    }

    [Fact]
    public void DefaultPath_IsUnderLocalAppDataFlarePurge()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlarePurge",
            "zones.v1.json");

        JsonZoneCacheStore.DefaultPath().Should().Be(expected);
    }
}
