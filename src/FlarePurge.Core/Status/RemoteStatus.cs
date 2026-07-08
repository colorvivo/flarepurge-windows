using System.Text.Json.Serialization;

namespace FlarePurge.Core.Status;

public sealed record RemoteStatus(
    [property: JsonPropertyName("disabled")] bool Disabled = false,
    [property: JsonPropertyName("message")] string? Message = null)
{
    public static readonly RemoteStatus Enabled = new();
    public bool IsEnabled => !Disabled;
}
