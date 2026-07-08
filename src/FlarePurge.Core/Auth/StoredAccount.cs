using System;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Auth;

public sealed record StoredAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("cloudflare_account_id")] string? CloudflareAccountId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("token_keychain_account")] string TokenKeychainAccount,
    [property: JsonPropertyName("added_at")] DateTimeOffset AddedAt);
