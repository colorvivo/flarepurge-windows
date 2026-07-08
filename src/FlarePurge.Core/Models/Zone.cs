using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Models;

[JsonConverter(typeof(ZoneJsonConverter))]
public sealed record Zone(
    string Id,
    string Name,
    string Status,
    IReadOnlyList<string>? NameServers,
    string? AccountId,
    string? AccountName,
    string? PlanName,
    DateTimeOffset? CreatedOn)
{
    public bool IsActive => Status == "active";
}
