using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class EphemeralKeychainTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var kc = new EphemeralKeychain();

        kc.Save("token-a", "user-1");

        kc.LoadToken("user-1").Should().Be("token-a");
    }

    [Fact]
    public void Load_MissingAccount_ReturnsNull()
    {
        var kc = new EphemeralKeychain();

        kc.LoadToken("missing").Should().BeNull();
    }

    [Fact]
    public void Save_OverwritesExistingToken()
    {
        var kc = new EphemeralKeychain();
        kc.Save("old", "user-1");

        kc.Save("new", "user-1");

        kc.LoadToken("user-1").Should().Be("new");
    }

    [Fact]
    public void Delete_RemovesEntry()
    {
        var kc = new EphemeralKeychain();
        kc.Save("t", "u");

        kc.Delete("u");

        kc.LoadToken("u").Should().BeNull();
    }

    [Fact]
    public void Delete_UnknownAccount_NoThrow()
    {
        var kc = new EphemeralKeychain();

        var act = () => kc.Delete("never-saved");

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteAll_ClearsEveryEntry()
    {
        var kc = new EphemeralKeychain();
        kc.Save("t1", "u1");
        kc.Save("t2", "u2");

        kc.DeleteAll();

        kc.ListAccounts().Should().BeEmpty();
    }

    [Fact]
    public void ListAccounts_ReturnsAllStoredKeys()
    {
        var kc = new EphemeralKeychain();
        kc.Save("t1", "u1");
        kc.Save("t2", "u2");

        kc.ListAccounts().Should().BeEquivalentTo(new[] { "u1", "u2" });
    }
}
