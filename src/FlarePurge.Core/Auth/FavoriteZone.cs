using System;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Auth;

public sealed record FavoriteZone(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("account_id")] string? AccountId,
    [property: JsonPropertyName("added_at")] DateTimeOffset AddedAt);
