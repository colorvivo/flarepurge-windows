using System.Collections.Generic;
using FlarePurge.Core.Auth;

namespace FlarePurge.App.Demo;

internal sealed class DemoKeychain : IKeychainProvider
{
    private readonly Dictionary<string, string> _tokens = new()
    {
        ["demo-token-1"] = "demo-token-value-0000000000000000000000000000000000000000",
        ["demo-token-2"] = "demo-token-value-1111111111111111111111111111111111111111",
    };

    public void Save(string token, string forAccount) => _tokens[forAccount] = token;

    public string? LoadToken(string forAccount) =>
        _tokens.TryGetValue(forAccount, out var t) ? t : null;

    public void Delete(string account) => _tokens.Remove(account);

    public void DeleteAll() => _tokens.Clear();

    public IReadOnlyList<string> ListAccounts() => [.. _tokens.Keys];
}
