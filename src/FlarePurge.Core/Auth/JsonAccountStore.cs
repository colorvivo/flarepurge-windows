using System;
using System.Collections.Generic;
using System.IO;
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
            return JsonSerializer.Deserialize(stream, CoreJsonContext.Default.AccountStoreData)
                ?? new AccountStoreData([], null, null, null);
        }
        catch (JsonException)
        {
            // Corrupt file — treat as empty rather than crash the app on launch.
            return new AccountStoreData([], null, null, null);
        }
    }

    private void Write(AccountStoreData data)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var tempPath = _path + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, data, CoreJsonContext.Default.AccountStoreData);
        }
        File.Move(tempPath, _path, overwrite: true);
    }

    public static string DefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "FlarePurge", "accounts.v1.json");
    }
}
