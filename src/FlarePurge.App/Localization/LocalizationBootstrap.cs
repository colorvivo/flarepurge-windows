using FlarePurge.Core.Auth;
using FlarePurge.Core.Localization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FlarePurge.App.Localization;

internal static class LocalizationBootstrap
{
    private static readonly ResourceLoader Loader = new();

    // Called from App startup so every downstream Localizer.Get(...) call
    // is routed through the packaged .resw pri map. Also applies the
    // per-user language override (if set), which takes effect for this
    // process; UI already loaded won't re-localize without a restart.
    public static void Install(IAccountStore store)
    {
        ApplyLanguageOverride(store.GetPreferences().LanguageOverride);

        Localizer.SetResolver(key =>
        {
            var normalized = LocalizationKeys.Normalize(key);
            var value = Loader.GetString(normalized);
            return string.IsNullOrEmpty(value) ? key : value;
        });
    }

    private static void ApplyLanguageOverride(string code)
    {
        if (string.IsNullOrEmpty(code) || code == "system")
        {
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            return;
        }
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = code;
    }
}
