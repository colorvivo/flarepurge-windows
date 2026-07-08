using System.Collections.Generic;

namespace FlarePurge.Core.Auth;

public interface IAccountStore
{
    IReadOnlyList<StoredAccount> LoadAccounts();
    void SaveAccounts(IReadOnlyList<StoredAccount> accounts);
    string? GetActiveAccountId();
    void SetActiveAccountId(string? id);
    Preferences GetPreferences();
    void SavePreferences(Preferences preferences);
    IReadOnlyList<FavoriteZone> GetFavorites();
    void SaveFavorites(IReadOnlyList<FavoriteZone> favorites);

    /// <summary>
    /// Rename a stored account in place. No-op if the account id is unknown,
    /// the label is empty/whitespace, or the label equals the current one.
    /// Returns true if the store was updated. Centralizes the rename path so
    /// views don't mutate the account list ad-hoc (paridad con Apple's
    /// <c>AppState.renameAccount</c>).
    /// </summary>
    bool RenameAccount(string id, string newLabel);
}
