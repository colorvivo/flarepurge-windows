using System.Text.Json.Serialization;

namespace FlarePurge.Core.Models;

public sealed record Account(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string? Type = null);
