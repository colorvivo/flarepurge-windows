using System;
using System.IO;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class FavoritesTests : IDisposable
{
    private readonly string _path;

    public FavoritesTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"flarepurge-favs-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        var tmp = _path + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    [Fact]
    public void GetFavorites_NoFile_ReturnsEmpty()
    {
        var store = new JsonAccountStore(_path);

        store.GetFavorites().Should().BeEmpty();
    }

    [Fact]
    public void SaveFavorites_ThenGet_RoundTrips()
    {
        var store = new JsonAccountStore(_path);
        var favs = new[]
        {
            new FavoriteZone("z1", "example.com", "acc-1", new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
            new FavoriteZone("z2", "otros.com", null, new DateTimeOffset(2026, 2, 2, 12, 0, 0, TimeSpan.Zero)),
        };

        store.SaveFavorites(favs);

        new JsonAccountStore(_path).GetFavorites().Should().BeEquivalentTo(favs);
    }

    [Fact]
    public void SaveFavorites_PreservesAccountsPrefsAndActiveId()
    {
        var store = new JsonAccountStore(_path);
        var accounts = new[] { new StoredAccount("1", "cf-1", "A", "k", DateTimeOffset.UtcNow) };
        var prefs = new Preferences(ConfirmPurgeEverything: false, ConfirmBulkPurge: true);
        store.SaveAccounts(accounts);
        store.SetActiveAccountId("1");
        store.SavePreferences(prefs);

        store.SaveFavorites([new FavoriteZone("z", "a.com", "cf-1", DateTimeOffset.UtcNow)]);

        store.LoadAccounts().Should().BeEquivalentTo(accounts);
        store.GetActiveAccountId().Should().Be("1");
        store.GetPreferences().Should().Be(prefs);
    }

    [Fact]
    public void SaveAccounts_PreservesFavorites()
    {
        var store = new JsonAccountStore(_path);
        var favs = new[] { new FavoriteZone("z", "a.com", null, DateTimeOffset.UtcNow) };
        store.SaveFavorites(favs);

        store.SaveAccounts([new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow)]);

        store.GetFavorites().Should().BeEquivalentTo(favs);
    }

    [Fact]
    public void SavePreferences_PreservesFavorites()
    {
        var store = new JsonAccountStore(_path);
        var favs = new[] { new FavoriteZone("z", "a.com", null, DateTimeOffset.UtcNow) };
        store.SaveFavorites(favs);

        store.SavePreferences(new Preferences(ConfirmPurgeEverything: false, ConfirmBulkPurge: false));

        store.GetFavorites().Should().BeEquivalentTo(favs);
    }

    [Fact]
    public void SaveFavorites_Empty_ClearsStore()
    {
        var store = new JsonAccountStore(_path);
        store.SaveFavorites([new FavoriteZone("z", "a.com", null, DateTimeOffset.UtcNow)]);

        store.SaveFavorites([]);

        store.GetFavorites().Should().BeEmpty();
    }
}
