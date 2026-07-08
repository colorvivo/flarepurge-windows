using System;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FlarePurge.App.Localization;

namespace FlarePurge.App.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    public string AppName => "FlarePurge";

    // Deliberately Windows-specific + locale-neutral: same value on all
    // locales. Apple's `about_description` talks about "Apple devices".
    public string Tagline => "Purge your Cloudflare cache in three clicks.";

    public string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.2" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public string Copyright => $"© {DateTime.Now.Year} Color Vivo Internet, SL";

    public string Website => "https://flarepurge.com";

    public string SupportEmail => "support@flarepurge.com";

    public string PrivacyUrl => "https://flarepurge.com/privacy";

    public string RepositoryUrl => "https://github.com/colorvivo/flarepurge-windows";

    // Info-table rows (paridad Mac v1.7.x — §8 About). Values are locale-neutral
    // technical labels ("Cloudflare v4 API", "DPAPI") except for Tracking and Price
    // which pull from Apple keys.
    public string BuiltWith => "C# 13 · WinUI 3";
    public string Backend => "Cloudflare v4 API";
    public string Tracking => L.S("mac_about_spec_tracking_value");
    public string TokenStorage => "Windows Credential Vault · DPAPI";
    public string Languages => L.S("mac_about_spec_languages_value");
    public string Price => L.S("mac_about_spec_price_value");

    public string MadeWith => L.S("mac_about_madeWith");

    public string CloudflareDisclaimer => L.S("mac_about_disclaimer");
}
