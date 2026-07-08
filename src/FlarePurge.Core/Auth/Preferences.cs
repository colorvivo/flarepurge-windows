using System.Text.Json.Serialization;

namespace FlarePurge.Core.Auth;

public sealed record Preferences(
    [property: JsonPropertyName("confirm_purge_everything")] bool ConfirmPurgeEverything = true,
    [property: JsonPropertyName("confirm_bulk_purge")] bool ConfirmBulkPurge = true,
    [property: JsonPropertyName("minimize_to_tray")] bool MinimizeToTray = false,
    [property: JsonPropertyName("theme_mode")] string ThemeMode = "auto",
    [property: JsonPropertyName("language_override")] string LanguageOverride = "system")
{
    public static readonly Preferences Default = new();
}
