using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlarePurge.Core.Auth;

public static class TokenProviderFactory
{
    // Composes the IApiClient token provider delegate from an IAccountStore
    // (active account selection) and an IKeychainProvider (secret fetch). Kept
    // in Core — it's pure logic and therefore unit-testable without the WinUI
    // runtime. No active account or no matching entry resolves to null, which
    // ApiClient forwards as "no Authorization header".
    public static Func<CancellationToken, ValueTask<string?>> FromActiveAccount(
        IAccountStore store,
        IKeychainProvider keychain)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keychain);

        return _ =>
        {
            var activeId = store.GetActiveAccountId();
            if (activeId is null) return ValueTask.FromResult<string?>(null);

            foreach (var account in store.LoadAccounts())
            {
                if (string.Equals(account.Id, activeId, StringComparison.Ordinal))
                    return ValueTask.FromResult(keychain.LoadToken(account.TokenKeychainAccount));
            }
            return ValueTask.FromResult<string?>(null);
        };
    }
}
