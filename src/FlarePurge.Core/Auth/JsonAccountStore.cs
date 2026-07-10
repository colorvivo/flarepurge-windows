using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlarePurge.Core.Json;

namespace FlarePurge.Core.Auth;

public sealed class JsonAccountStore : IAccountStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonAccountStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public IReadOnlyList<StoredAccount> LoadAccounts()
    {
        lock (_lock) { return Load().Accounts; }
    }

    public void SaveAccounts(IReadOnlyList<StoredAccount> accounts)
    {
        lock (_lock)
        {
            var current = Load();
            Write(current with { Accounts = accounts });
        }
    }

    public string? GetActiveAccountId()
    {
        lock (_lock) { return Load().ActiveAccountId; }
    }

    public void SetActiveAccountId(string? id)
    {
        lock (_lock)
        {
            var current = Load();
            Write(current with { ActiveAccountId = id });
        }
    }

    public Preferences GetPreferences()
    {
        lock (_lock) { return Load().Preferences ?? Preferences.Default; }
    }

    public void SavePreferences(Preferences preferences)
    {
        lock (_lock)
        {
            var current = Load();
            Write(current with { Preferences = preferences });
        }
    }

    public IReadOnlyList<FavoriteZone> GetFavorites()
    {
        lock (_lock) { return Load().Favorites ?? []; }
    }

    public void SaveFavorites(IReadOnlyList<FavoriteZone> favorites)
    {
        lock (_lock)
        {
            var current = Load();
            Write(current with { Favorites = favorites });
        }
    }

    public bool RenameAccount(string id, string newLabel)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var trimmed = newLabel?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return false;

        lock (_lock)
        {
            var current = Load();
            var found = false;
            var updated = new StoredAccount[current.Accounts.Count];
            for (int i = 0; i < current.Accounts.Count; i++)
            {
                var a = current.Accounts[i];
                if (a.Id == id && !string.Equals(a.Label, trimmed, System.StringComparison.Ordinal))
                {
                    updated[i] = a with { Label = trimmed };
                    found = true;
                }
                else
                {
                    updated[i] = a;
                }
            }
            if (!found) return false;
            Write(current with { Accounts = updated });
            return true;
        }
    }

    private AccountStoreData Load()
    {
        if (!File.Exists(_path)) return new AccountStoreData([], null, null, null);
        try
        {
            using var stream = File.OpenRead(_path);
            var data = JsonSerializer.Deserialize(stream, CoreJsonContext.Default.AccountStoreData);
            if (data is null) return new AccountStoreData([], null, null, null);
            // A syntactically valid file may still have "accounts": null (STJ does
            // not enforce the record's non-nullability); a null list NREs downstream.
            return data.Accounts is null ? data with { Accounts = [] } : data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable (bad JSON, AV/backup lock, or another instance's
            // sharing violation) — degrade to empty rather than crash at launch,
            // where Load() runs in the MainWindow constructor.
            return new AccountStoreData([], null, null, null);
        }
    }

    /// <summary>
    /// Emits the token-vault slots referenced by the stored accounts, but ONLY
    /// when the accounts file was read with certainty. Returns <c>false</c> — and
    /// callers must not act on <paramref name="slots"/> — when the file is absent
    /// or could not be read/parsed. This is the safety gate for
    /// <see cref="KeychainReconciler"/> (audit G2): a transient AV/backup lock
    /// makes <see cref="Load"/> degrade to "no accounts", and reconciling on that
    /// would wipe live tokens. Only a positively-parsed file is trustworthy.
    /// </summary>
    public bool TryLoadReferencedTokenSlots(out IReadOnlyList<string> slots)
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) { slots = []; return false; }
            try
            {
                using var stream = File.OpenRead(_path);
                var data = JsonSerializer.Deserialize(stream, CoreJsonContext.Default.AccountStoreData);
                var accounts = data?.Accounts ?? [];
                slots = accounts.Select(a => a.TokenKeychainAccount).ToArray();
                return true;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                slots = [];
                return false;
            }
        }
    }

    private void Write(AccountStoreData data)
        => AtomicFile.Write(_path, stream =>
            JsonSerializer.Serialize(stream, data, CoreJsonContext.Default.AccountStoreData));

    public static string DefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "FlarePurge", "accounts.v1.json");
    }
}
