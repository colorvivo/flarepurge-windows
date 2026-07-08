using System.Text.Json.Serialization;

namespace FlarePurge.Core.Models;

public sealed record TokenVerification(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status)
{
    public bool IsActive => Status == "active";
}
