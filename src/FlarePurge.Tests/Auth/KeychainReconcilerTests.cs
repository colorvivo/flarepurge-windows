using System;
using System.IO;
using System.Linq;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

// Audit G2: prune orphaned vault tokens — but NEVER on a degraded read, which
// would wipe live tokens. These tests pin the safety gate.
public class KeychainReconcilerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public KeychainReconcilerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fp-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "accounts.v1.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private JsonAccountStore StoreWith(params string[] tokenSlots)
    {
        var store = new JsonAccountStore(_path);
        store.SaveAccounts(tokenSlots
            .Select((slot, i) => new StoredAccount($"id{i}", null, $"acc{i}", slot, DateTimeOffset.UtcNow))
            .ToArray());
        return store;
    }

    [Fact]
    public void Reconcile_RemovesOrphanKeepsReferenced()
    {
        var store = StoreWith("kc-referenced");
        var keychain = new EphemeralKeychain();
        keychain.Save("live-token", "kc-referenced");
        keychain.Save("stale-token", "kc-orphan");

        var removed = KeychainReconciler.Reconcile(store, keychain);

        removed.Should().Be(1);
        keychain.ListAccounts().Should().BeEquivalentTo(new[] { "kc-referenced" });
    }

    [Fact]
    public void Reconcile_CorruptFile_DeletesNothing()
    {
        // The dangerous case: a degraded read must never wipe live tokens.
        File.WriteAllText(_path, "{ this is not valid json");
        var store = new JsonAccountStore(_path);
        var keychain = new EphemeralKeychain();
        keychain.Save("t1", "kc-1");
        keychain.Save("t2", "kc-2");

        var removed = KeychainReconciler.Reconcile(store, keychain);

        removed.Should().Be(0);
        keychain.ListAccounts().Should().BeEquivalentTo(new[] { "kc-1", "kc-2" });
    }

    [Fact]
    public void Reconcile_AbsentFile_DeletesNothing()
    {
        var store = new JsonAccountStore(_path); // file never created
        var keychain = new EphemeralKeychain();
        keychain.Save("t1", "kc-1");

        var removed = KeychainReconciler.Reconcile(store, keychain);

        removed.Should().Be(0);
        keychain.ListAccounts().Should().BeEquivalentTo(new[] { "kc-1" });
    }

    [Fact]
    public void TryLoadReferencedTokenSlots_ValidFile_ReturnsTrueWithSlots()
    {
        var store = StoreWith("kc-a", "kc-b");

        store.TryLoadReferencedTokenSlots(out var slots).Should().BeTrue();
        slots.Should().BeEquivalentTo(new[] { "kc-a", "kc-b" });
    }

    [Fact]
    public void TryLoadReferencedTokenSlots_CorruptFile_ReturnsFalse()
    {
        File.WriteAllText(_path, "not json");
        var store = new JsonAccountStore(_path);

        store.TryLoadReferencedTokenSlots(out _).Should().BeFalse();
    }

    [Fact]
    public void TryLoadReferencedTokenSlots_AbsentFile_ReturnsFalse()
    {
        var store = new JsonAccountStore(_path);

        store.TryLoadReferencedTokenSlots(out _).Should().BeFalse();
    }
}
