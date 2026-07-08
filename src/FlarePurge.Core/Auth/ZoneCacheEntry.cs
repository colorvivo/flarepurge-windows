using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Auth;

public sealed record CachedZone(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("name_servers")] IReadOnlyList<string>? NameServers,
    [property: JsonPropertyName("account_id")] string? AccountId,
    [property: JsonPropertyName("account_name")] string? AccountName,
    [property: JsonPropertyName("plan_name")] string? PlanName,
    [property: JsonPropertyName("created_on")] DateTimeOffset? CreatedOn)
{
    public static CachedZone From(Zone z)
        => new(z.Id, z.Name, z.Status, z.NameServers, z.AccountId, z.AccountName, z.PlanName, z.CreatedOn);

    public Zone ToZone()
        => new(Id, Name, Status, NameServers, AccountId, AccountName, PlanName, CreatedOn);
}

public sealed record ZoneCacheEntry(
    [property: JsonPropertyName("account_id")] string AccountId,
    [property: JsonPropertyName("fetched_at")] DateTimeOffset FetchedAt,
    [property: JsonPropertyName("zones")] IReadOnlyList<CachedZone> Zones);

public sealed record ZoneCacheData(
    [property: JsonPropertyName("entries")] IReadOnlyList<ZoneCacheEntry> Entries);
