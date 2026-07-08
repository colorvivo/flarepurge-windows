using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Auth;

public sealed record AccountStoreData(
    [property: JsonPropertyName("accounts")] IReadOnlyList<StoredAccount> Accounts,
    [property: JsonPropertyName("active_account_id")] string? ActiveAccountId,
    [property: JsonPropertyName("preferences")] Preferences? Preferences = null,
    [property: JsonPropertyName("favorites")] IReadOnlyList<FavoriteZone>? Favorites = null);
