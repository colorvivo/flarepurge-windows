using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Security.Credentials;

namespace FlarePurge.Core.Auth;

// PasswordVault encrypts entries with DPAPI tied to the Windows user profile,
// equivalent to iOS's kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly. The
// vault is per-user — a second Windows account on the same machine cannot read
// another user's credentials. Sync to Microsoft Account is opt-in and mirrors
// the default non-synced iOS behaviour.
//
// Requires Package Identity (MSIX). Tests use EphemeralKeychain instead.
public sealed class WindowsCredentialKeychain : IKeychainProvider
{
    private const string Resource = "com.colorvivo.flarepurge";
    private readonly PasswordVault _vault = new();

    public void Save(string token, string forAccount)
    {
        TryDelete(forAccount);
        _vault.Add(new PasswordCredential(Resource, forAccount, token));
    }

    public string? LoadToken(string forAccount)
    {
        try
        {
            var credential = _vault.Retrieve(Resource, forAccount);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public void Delete(string account) => TryDelete(account);

    public void DeleteAll()
    {
        try
        {
            foreach (var credential in _vault.FindAllByResource(Resource).ToArray())
                _vault.Remove(credential);
        }
        catch (COMException)
        {
            // Vault is empty — nothing to remove.
        }
    }

    public IReadOnlyList<string> ListAccounts()
    {
        try
        {
            return _vault.FindAllByResource(Resource).Select(c => c.UserName).ToArray();
        }
        catch (COMException)
        {
            return [];
        }
    }

    private void TryDelete(string account)
    {
        try
        {
            var credential = _vault.Retrieve(Resource, account);
            _vault.Remove(credential);
        }
        catch (COMException)
        {
            // Entry did not exist — nothing to remove.
        }
    }
}
