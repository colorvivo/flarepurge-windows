using System.Collections.Generic;
using FlarePurge.Core.Auth;

namespace FlarePurge.App.Demo;

/// <summary>
/// In-memory, pre-seeded IAccountStore used under -FPDemoMode 1. No file I/O,
/// no keychain. Values reset on process exit so demo runs are reproducible.
/// </summary>
internal sealed class InMemoryAccountStore : IAccountStore
{
    private readonly object _lock = new();
    private List<StoredAccount> _accounts;
    private string? _activeId;
    private Preferences _prefs;
    private List<FavoriteZone> _favorites;

    public InMemoryAccountStore()
    {
        _accounts = [.. DemoData.StoredAccounts];
        _activeId = DemoData.DefaultActiveStoredAccountId;
        _prefs = Preferences.Default with { ThemeMode = "auto", LanguageOverride = "system" };
        _favorites = [.. DemoData.Favorites];
    }

    public IReadOnlyList<StoredAccount> LoadAccounts()
    {
        lock (_lock) return _accounts.ToArray();
    }

    public void SaveAccounts(IReadOnlyList<StoredAccount> accounts)
    {
        lock (_lock) _accounts = [.. accounts];
    }

    public string? GetActiveAccountId()
    {
        lock (_lock) return _activeId;
    }

    public void SetActiveAccountId(string? id)
    {
        lock (_lock) _activeId = id;
    }

    public Preferences GetPreferences()
    {
        lock (_lock) return _prefs;
    }

    public void SavePreferences(Preferences preferences)
    {
        lock (_lock) _prefs = preferences;
    }

    public IReadOnlyList<FavoriteZone> GetFavorites()
    {
        lock (_lock) return _favorites.ToArray();
    }

    public void SaveFavorites(IReadOnlyList<FavoriteZone> favorites)
    {
        lock (_lock) _favorites = [.. favorites];
    }

    public bool RenameAccount(string id, string newLabel)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var trimmed = newLabel?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return false;

        lock (_lock)
        {
            for (int i = 0; i < _accounts.Count; i++)
            {
                if (_accounts[i].Id == id && !string.Equals(_accounts[i].Label, trimmed, System.StringComparison.Ordinal))
                {
                    _accounts[i] = _accounts[i] with { Label = trimmed };
                    return true;
                }
            }
            return false;
        }
    }
}
