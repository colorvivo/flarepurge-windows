using System;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class TokenProviderFactoryTests
{
    private sealed class InMemoryAccountStore : IAccountStore
    {
        public string? ActiveId { get; set; }
        public System.Collections.Generic.List<StoredAccount> Accounts { get; } = new();
        public Preferences Preferences { get; set; } = Preferences.Default;
        public System.Collections.Generic.List<FavoriteZone> Favorites { get; } = new();
        public System.Collections.Generic.IReadOnlyList<StoredAccount> LoadAccounts() => Accounts;
        public void SaveAccounts(System.Collections.Generic.IReadOnlyList<StoredAccount> accounts) { Accounts.Clear(); Accounts.AddRange(accounts); }
        public string? GetActiveAccountId() => ActiveId;
        public void SetActiveAccountId(string? id) => ActiveId = id;
        public Preferences GetPreferences() => Preferences;
        public void SavePreferences(Preferences preferences) => Preferences = preferences;
        public System.Collections.Generic.IReadOnlyList<FavoriteZone> GetFavorites() => Favorites;
        public void SaveFavorites(System.Collections.Generic.IReadOnlyList<FavoriteZone> favorites) { Favorites.Clear(); Favorites.AddRange(favorites); }
        public bool RenameAccount(string id, string newLabel)
        {
            var trimmed = newLabel?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id) || trimmed.Length == 0) return false;
            for (int i = 0; i < Accounts.Count; i++)
            {
                if (Accounts[i].Id == id && Accounts[i].Label != trimmed)
                {
                    Accounts[i] = Accounts[i] with { Label = trimmed };
                    return true;
                }
            }
            return false;
        }
    }

    [Fact]
    public async Task NoActiveAccount_ReturnsNull()
    {
        var store = new InMemoryAccountStore();
        var kc = new EphemeralKeychain();
        var provider = TokenProviderFactory.FromActiveAccount(store, kc);

        (await provider(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ActiveAccountButNoMatchingEntry_ReturnsNull()
    {
        var store = new InMemoryAccountStore { ActiveId = "missing" };
        var kc = new EphemeralKeychain();
        var provider = TokenProviderFactory.FromActiveAccount(store, kc);

        (await provider(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ActiveAccountResolvesTokenFromKeychain()
    {
        var store = new InMemoryAccountStore
        {
            ActiveId = "acc-1",
            Accounts =
            {
                new StoredAccount("acc-1", null, "Personal", "kc-slot-1", DateTimeOffset.UtcNow),
                new StoredAccount("acc-2", null, "Work", "kc-slot-2", DateTimeOffset.UtcNow),
            },
        };
        var kc = new EphemeralKeychain();
        kc.Save("token-one", "kc-slot-1");
        kc.Save("token-two", "kc-slot-2");
        var provider = TokenProviderFactory.FromActiveAccount(store, kc);

        (await provider(CancellationToken.None)).Should().Be("token-one");
    }

    [Fact]
    public async Task ActiveAccountWithoutKeychainEntry_ReturnsNull()
    {
        var store = new InMemoryAccountStore
        {
            ActiveId = "acc-1",
            Accounts = { new StoredAccount("acc-1", null, "a", "missing-slot", DateTimeOffset.UtcNow) },
        };
        var provider = TokenProviderFactory.FromActiveAccount(store, new EphemeralKeychain());

        (await provider(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var store = new InMemoryAccountStore();
        var kc = new EphemeralKeychain();

        ((Action)(() => TokenProviderFactory.FromActiveAccount(null!, kc))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TokenProviderFactory.FromActiveAccount(store, null!))).Should().Throw<ArgumentNullException>();
    }

    // --- C4: FromAccountId resolves a SPECIFIC account's token, ignoring active ---

    [Fact]
    public async Task FromAccountId_ResolvesTargetAccount_IgnoringActive()
    {
        var store = new InMemoryAccountStore
        {
            ActiveId = "acc-1", // active is acc-1, but we ask for acc-2
            Accounts =
            {
                new StoredAccount("acc-1", null, "Personal", "kc-slot-1", DateTimeOffset.UtcNow),
                new StoredAccount("acc-2", null, "Work", "kc-slot-2", DateTimeOffset.UtcNow),
            },
        };
        var kc = new EphemeralKeychain();
        kc.Save("token-one", "kc-slot-1");
        kc.Save("token-two", "kc-slot-2");
        var provider = TokenProviderFactory.FromAccountId(store, kc, "acc-2");

        (await provider(CancellationToken.None)).Should().Be("token-two");
    }

    [Fact]
    public async Task FromAccountId_UnknownAccount_ReturnsNull()
    {
        var store = new InMemoryAccountStore
        {
            Accounts = { new StoredAccount("acc-1", null, "a", "kc-slot-1", DateTimeOffset.UtcNow) },
        };
        var kc = new EphemeralKeychain();
        kc.Save("token-one", "kc-slot-1");
        var provider = TokenProviderFactory.FromAccountId(store, kc, "acc-nonexistent");

        (await provider(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void FromAccountId_NullArguments_Throw()
    {
        var store = new InMemoryAccountStore();
        var kc = new EphemeralKeychain();

        ((Action)(() => TokenProviderFactory.FromAccountId(null!, kc, "x"))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TokenProviderFactory.FromAccountId(store, null!, "x"))).Should().Throw<ArgumentNullException>();
    }
}
