using System;
using System.IO;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class PreferencesTests : IDisposable
{
    private readonly string _path;

    public PreferencesTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"flarepurge-prefs-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        var tmp = _path + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    [Fact]
    public void Default_BothConfirmationsEnabled()
    {
        Preferences.Default.ConfirmPurgeEverything.Should().BeTrue();
        Preferences.Default.ConfirmBulkPurge.Should().BeTrue();
    }

    [Fact]
    public void GetPreferences_NoFile_ReturnsDefaults()
    {
        var store = new JsonAccountStore(_path);

        store.GetPreferences().Should().Be(Preferences.Default);
    }

    [Fact]
    public void SavePreferences_ThenGet_RoundTrips()
    {
        var store = new JsonAccountStore(_path);
        var prefs = new Preferences(ConfirmPurgeEverything: false, ConfirmBulkPurge: false);

        store.SavePreferences(prefs);

        new JsonAccountStore(_path).GetPreferences().Should().Be(prefs);
    }

    [Fact]
    public void SavePreferences_PreservesAccountsAndActiveId()
    {
        var store = new JsonAccountStore(_path);
        var accounts = new[] { new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow) };
        store.SaveAccounts(accounts);
        store.SetActiveAccountId("1");

        store.SavePreferences(new Preferences(ConfirmPurgeEverything: false, ConfirmBulkPurge: true));

        store.LoadAccounts().Should().BeEquivalentTo(accounts);
        store.GetActiveAccountId().Should().Be("1");
    }

    [Fact]
    public void SaveAccounts_PreservesPreferences()
    {
        var store = new JsonAccountStore(_path);
        var prefs = new Preferences(ConfirmPurgeEverything: false, ConfirmBulkPurge: false);
        store.SavePreferences(prefs);

        store.SaveAccounts([new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow)]);

        store.GetPreferences().Should().Be(prefs);
    }
}
