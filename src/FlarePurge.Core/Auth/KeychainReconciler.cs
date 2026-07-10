using System;
using System.Linq;

namespace FlarePurge.Core.Auth;

/// <summary>
/// Removes token-vault entries that no stored account references, so a partially
/// lost <c>accounts.v1.json</c> doesn't leave tokens orphaned in the Credential
/// Vault (audit G2).
///
/// SAFETY: this only ever deletes when the accounts file parsed successfully (via
/// <see cref="JsonAccountStore.TryLoadReferencedTokenSlots"/>). A degraded read —
/// a transient antivirus/backup lock — makes the store look empty, and blindly
/// reconciling on that would wipe every live token. In that case we do nothing.
/// The vault enumeration is already scoped to FlarePurge's own resource, so this
/// never touches other apps' credentials.
/// </summary>
public static class KeychainReconciler
{
    public static int Reconcile(JsonAccountStore store, IKeychainProvider keychain)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keychain);

        if (!store.TryLoadReferencedTokenSlots(out var referenced))
            return 0; // absent or unreadable — never delete on an untrusted read.

        var keep = referenced.ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var slot in keychain.ListAccounts())
        {
            if (!keep.Contains(slot))
            {
                keychain.Delete(slot);
                removed++;
            }
        }
        return removed;
    }
}
