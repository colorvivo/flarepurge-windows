using System;
using System.IO;
using System.Threading.Tasks;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class JsonAccountStoreTests : IDisposable
{
    private readonly string _path;

    public JsonAccountStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"flarepurge-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        var tmp = _path + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    [Fact]
    public void LoadAccounts_NoFile_ReturnsEmpty()
    {
        var store = new JsonAccountStore(_path);

        store.LoadAccounts().Should().BeEmpty();
        store.GetActiveAccountId().Should().BeNull();
    }

    [Fact]
    public void SaveAccounts_ThenLoadAccounts_RoundTrips()
    {
        var store = new JsonAccountStore(_path);
        var accounts = new[]
        {
            new StoredAccount("1", "cf-1", "Personal", "keychain-1", new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero)),
            new StoredAccount("2", null, "Work", "keychain-2", new DateTimeOffset(2026, 4, 22, 11, 0, 0, TimeSpan.Zero)),
        };

        store.SaveAccounts(accounts);

        var reloaded = new JsonAccountStore(_path).LoadAccounts();
        reloaded.Should().BeEquivalentTo(accounts);
    }

    [Fact]
    public void SetActiveAccountId_ThenGet_RoundTrips()
    {
        var store = new JsonAccountStore(_path);

        store.SetActiveAccountId("acc-42");

        new JsonAccountStore(_path).GetActiveAccountId().Should().Be("acc-42");
    }

    [Fact]
    public void SetActiveAccountId_Null_Clears()
    {
        var store = new JsonAccountStore(_path);
        store.SetActiveAccountId("x");

        store.SetActiveAccountId(null);

        store.GetActiveAccountId().Should().BeNull();
    }

    [Fact]
    public void SaveAccounts_PreservesActiveAccountId()
    {
        var store = new JsonAccountStore(_path);
        store.SetActiveAccountId("x");

        store.SaveAccounts([new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow)]);

        store.GetActiveAccountId().Should().Be("x");
    }

    [Fact]
    public void SetActiveAccountId_PreservesAccounts()
    {
        var store = new JsonAccountStore(_path);
        var accounts = new[] { new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow) };
        store.SaveAccounts(accounts);

        store.SetActiveAccountId("1");

        store.LoadAccounts().Should().BeEquivalentTo(accounts);
    }

    [Fact]
    public void LoadAccounts_CorruptFile_ReturnsEmpty()
    {
        File.WriteAllText(_path, "{ this is not valid json");

        var store = new JsonAccountStore(_path);

        store.LoadAccounts().Should().BeEmpty();
        store.GetActiveAccountId().Should().BeNull();
    }

    [Fact]
    public void Concurrent_SaveAndLoad_Sequentialized()
    {
        var store = new JsonAccountStore(_path);
        var accounts = new[] { new StoredAccount("1", null, "a", "k", DateTimeOffset.UtcNow) };

        Parallel.For(0, 16, i =>
        {
            store.SaveAccounts(accounts);
            _ = store.LoadAccounts();
        });

        store.LoadAccounts().Should().BeEquivalentTo(accounts);
    }

    [Fact]
    public void RenameAccount_UpdatesLabel_ReturnsTrue()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([new StoredAccount("a1", "cf-1", "Old", "k1", DateTimeOffset.UtcNow)]);

        var ok = store.RenameAccount("a1", "New label");

        ok.Should().BeTrue();
        store.LoadAccounts().Single().Label.Should().Be("New label");
    }

    [Fact]
    public void RenameAccount_TrimsWhitespace()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([new StoredAccount("a1", null, "Old", "k1", DateTimeOffset.UtcNow)]);

        store.RenameAccount("a1", "  Work  ").Should().BeTrue();
        store.LoadAccounts().Single().Label.Should().Be("Work");
    }

    [Fact]
    public void RenameAccount_NoOpWhenLabelUnchanged_ReturnsFalse()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([new StoredAccount("a1", null, "Same", "k1", DateTimeOffset.UtcNow)]);

        store.RenameAccount("a1", "Same").Should().BeFalse();
    }

    [Fact]
    public void RenameAccount_RejectsEmptyLabel_ReturnsFalse()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([new StoredAccount("a1", null, "Keep", "k1", DateTimeOffset.UtcNow)]);

        store.RenameAccount("a1", "").Should().BeFalse();
        store.RenameAccount("a1", "   ").Should().BeFalse();
        store.LoadAccounts().Single().Label.Should().Be("Keep");
    }

    [Fact]
    public void RenameAccount_UnknownId_ReturnsFalse()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([new StoredAccount("a1", null, "X", "k1", DateTimeOffset.UtcNow)]);

        store.RenameAccount("nope", "anything").Should().BeFalse();
    }

    [Fact]
    public void RenameAccount_DoesNotMutateOtherAccounts()
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts([
            new StoredAccount("a1", null, "Alice", "k1", DateTimeOffset.UtcNow),
            new StoredAccount("a2", null, "Bob", "k2", DateTimeOffset.UtcNow),
        ]);

        store.RenameAccount("a1", "Alice Pro").Should().BeTrue();

        var all = store.LoadAccounts();
        all.Should().HaveCount(2);
        all.First(a => a.Id == "a1").Label.Should().Be("Alice Pro");
        all.First(a => a.Id == "a2").Label.Should().Be("Bob");
    }

    [Fact]
    public void DefaultPath_IsUnderLocalAppDataFlarePurge()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlarePurge",
            "accounts.v1.json");

        JsonAccountStore.DefaultPath().Should().Be(expected);
    }
}
