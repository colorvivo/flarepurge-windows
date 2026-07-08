using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FlarePurge.Core.Auth;

public sealed class EphemeralKeychain : IKeychainProvider
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public void Save(string token, string forAccount) => _store[forAccount] = token;

    public string? LoadToken(string forAccount)
        => _store.TryGetValue(forAccount, out var token) ? token : null;

    public void Delete(string account) => _store.TryRemove(account, out _);

    public void DeleteAll() => _store.Clear();

    public IReadOnlyList<string> ListAccounts() => _store.Keys.ToArray();
}
