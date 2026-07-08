using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FlarePurge.App.Localization;
using FlarePurge.Core.Auth;

namespace FlarePurge.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAccountStore _store;

    public static event Action<string>? ThemeChanged;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption("auto", L.S("settings_colorScheme_system")),
        new ThemeOption("light", L.S("settings_colorScheme_light")),
        new ThemeOption("dark", L.S("settings_colorScheme_dark")),
    };

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[]
    {
        new LanguageOption("system", L.S("settings_language_system")),
        new LanguageOption("en", "English"),
        new LanguageOption("es", "Español"),
        new LanguageOption("es-MX", "Español (México)"),
        new LanguageOption("ca", "Català"),
        new LanguageOption("fr", "Français"),
        new LanguageOption("pt-PT", "Português"),
        new LanguageOption("it", "Italiano"),
        new LanguageOption("de", "Deutsch"),
        new LanguageOption("nl", "Nederlands"),
        new LanguageOption("nb", "Norsk bokmål"),
        new LanguageOption("sv", "Svenska"),
        new LanguageOption("zh-Hans", "中文(简体)"),
        new LanguageOption("ko", "한국어"),
        new LanguageOption("ja", "日本語"),
        new LanguageOption("ar", "العربية"),
        new LanguageOption("ro", "Română"),
        new LanguageOption("pl", "Polski"),
        new LanguageOption("th", "ไทย"),
        new LanguageOption("hu", "Magyar"),
        new LanguageOption("el", "Ελληνικά"),
        new LanguageOption("he", "עברית"),
    };

    [ObservableProperty]
    private bool _confirmPurgeEverything = true;

    [ObservableProperty]
    private bool _confirmBulkPurge = true;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    public SettingsViewModel(IAccountStore store)
    {
        _store = store;
        var prefs = _store.GetPreferences();
        _confirmPurgeEverything = prefs.ConfirmPurgeEverything;
        _confirmBulkPurge = prefs.ConfirmBulkPurge;
        _minimizeToTray = prefs.MinimizeToTray;
        _selectedTheme = FindOrDefault(ThemeOptions, o => o.Code == prefs.ThemeMode) ?? ThemeOptions[0];
        _selectedLanguage = FindOrDefault(LanguageOptions, o => o.Code == prefs.LanguageOverride) ?? LanguageOptions[0];
    }

    partial void OnConfirmPurgeEverythingChanged(bool value) => Persist();
    partial void OnConfirmBulkPurgeChanged(bool value) => Persist();
    partial void OnMinimizeToTrayChanged(bool value) => Persist();
    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        Persist();
        if (value is not null) ThemeChanged?.Invoke(value.Code);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value) => Persist();

    private void Persist()
        => _store.SavePreferences(new Preferences(
            ConfirmPurgeEverything: ConfirmPurgeEverything,
            ConfirmBulkPurge: ConfirmBulkPurge,
            MinimizeToTray: MinimizeToTray,
            ThemeMode: SelectedTheme?.Code ?? "auto",
            LanguageOverride: SelectedLanguage?.Code ?? "system"));

    private static T? FindOrDefault<T>(IReadOnlyList<T> list, System.Func<T, bool> predicate) where T : class
    {
        foreach (var item in list) if (predicate(item)) return item;
        return null;
    }
}

public sealed record ThemeOption(string Code, string Display)
{
    public override string ToString() => Display;
}

public sealed record LanguageOption(string Code, string Display)
{
    public override string ToString() => Display;
}
