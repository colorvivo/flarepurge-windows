using System;
using System.Globalization;
using System.Threading.Tasks;
using FlarePurge.App.Localization;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Purge;
using FlarePurge.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace FlarePurge.App.Views;

internal static class AppDialogs
{
    public static async Task<bool> ShowPurgeConfirmAsync(
        XamlRoot root,
        string title,
        string description,
        string? primaryLabel = null)
    {
        primaryLabel ??= L.S("action_confirm");
        var tcs = new TaskCompletionSource<bool>();

        var content = new StackPanel
        {
            Spacing = 14,
            MinWidth = 340,
            MaxWidth = 380,
            Padding = new Thickness(4, 8, 4, 4),
        };

        var halo = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(32),
            Background = ThemeBrush(root, "FPAccentSubtleBrush") ?? new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new FontIcon
            {
                Glyph = "", // flame
                FontSize = 30,
                Foreground = ThemeBrush(root, "FPAccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        content.Children.Add(halo);

        content.Children.Add(new TextBlock
        {
            Text = title,
            Style = TryStyle("FPH3Style"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        content.Children.Add(new TextBlock
        {
            Text = description,
            Style = TryStyle("FPBodyStyle"),
            Foreground = ThemeBrush(root, "FPTextSecondaryBrush"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Pill "NO SE PUEDE DESHACER"
        var pillContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        pillContent.Children.Add(new FontIcon
        {
            Glyph = "", // warning
            FontSize = 10,
            Foreground = ThemeBrush(root, "FPAccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        pillContent.Children.Add(new TextBlock
        {
            Text = L.S("pill_undoable"),
            Style = TryStyle("FPPillTextStyle"),
            Foreground = ThemeBrush(root, "FPAccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var pill = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(999),
            Background = ThemeBrush(root, "FPAccentSubtleBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = pillContent,
        };
        content.Children.Add(pill);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Content = content,
            PrimaryButtonText = primaryLabel,
            CloseButtonText = L.S("action_cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var style = Application.Current.Resources["AccentButtonStyle"] as Style;
        if (style is not null) dialog.PrimaryButtonStyle = style;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public static async Task<SettingsDialogResult> ShowSettingsAsync(XamlRoot root)
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        var view = new SettingsView(vm) { IsModalChild = true };
        var outcome = new SettingsDialogResult();

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = L.S("settings_title"),
            Content = view,
            CloseButtonText = L.S("action_done"),
            DefaultButton = ContentDialogButton.Close,
        };
        // Widen beyond the default ~548px cap so the tab content doesn't clip combo boxes / toggles.
        dialog.Resources["ContentDialogMaxWidth"] = 720.0;
        dialog.Resources["ContentDialogMinWidth"] = 640.0;

        var style = Application.Current.Resources["AccentButtonStyle"] as Style;
        if (style is not null) dialog.CloseButtonStyle = style;

        view.AddAccountRequested += (_, _) => { outcome.AddAccount = true; dialog.Hide(); };
        view.AccountsChanged += (_, _) => { outcome.AccountsChanged = true; };

        await dialog.ShowAsync();
        return outcome;
    }

    public static async Task ShowAboutAsync(XamlRoot root)
    {
        var vm = App.Services.GetRequiredService<AboutViewModel>();
        var view = new AboutView(vm) { IsModalChild = true };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Content = view,
            CloseButtonText = L.S("action_done"),
        };
        await dialog.ShowAsync();
    }

    public static async Task<SelectivePurgeResult?> ShowSelectivePurgeAsync(
        XamlRoot root,
        ZoneDisplayItem zone,
        ICacheService cache)
    {
        var tcs = new TaskCompletionSource<SelectivePurgeResult?>();

        var pivot = new Pivot
        {
            MinWidth = 460,
            MinHeight = 220,
        };

        var urlInput = new TextBox
        {
            PlaceholderText = "https://example.com/page\nhttps://example.com/image.png",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)Application.Current.Resources["FPFontMono"],
            MinHeight = 120,
            MaxHeight = 200,
        };
        var urlHint = new TextBlock
        {
            Text = L.S("selective_urlHint"),
            Style = TryStyle("FPCaptionStyle"),
            Foreground = ThemeBrush(root, "FPTextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        };
        var urlCounter = new TextBlock
        {
            Text = L.Format("selective_urlCounterFmt", 0),
            Style = TryStyle("FPMonoSmallStyle"),
            Foreground = ThemeBrush(root, "FPTextTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        urlInput.TextChanged += (_, _) =>
        {
            var n = SplitLines(urlInput.Text).Length;
            urlCounter.Text = L.Format("selective_urlCounterFmt", n);
            urlCounter.Foreground = n > 30
                ? ThemeBrush(root, "FPAccentBrush")
                : ThemeBrush(root, "FPTextTertiaryBrush");
        };
        var urlHeaderRow = new Grid();
        urlHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        urlHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(urlHint, 0);
        Grid.SetColumn(urlCounter, 1);
        urlHeaderRow.Children.Add(urlHint);
        urlHeaderRow.Children.Add(urlCounter);

        var urlsPanel = new StackPanel { Spacing = 8 };
        urlsPanel.Children.Add(urlHeaderRow);
        urlsPanel.Children.Add(urlInput);
        pivot.Items.Add(new PivotItem { Header = L.S("selective_tagUrls"), Content = urlsPanel });

        var hostInput = new TextBox
        {
            PlaceholderText = "cdn.example.com\nstatic.example.com",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)Application.Current.Resources["FPFontMono"],
            MinHeight = 120,
            MaxHeight = 200,
        };
        var hostHint = new TextBlock
        {
            Text = L.S("selective_hostHint"),
            Style = TryStyle("FPCaptionStyle"),
            Foreground = ThemeBrush(root, "FPTextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        };
        var hostsPanel = new StackPanel { Spacing = 8 };
        hostsPanel.Children.Add(hostHint);
        hostsPanel.Children.Add(hostInput);
        pivot.Items.Add(new PivotItem { Header = L.S("selective_tagHosts"), Content = hostsPanel });

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = L.Format("selective_dialogTitleFmt", zone.Name),
            Content = pivot,
            PrimaryButtonText = L.S("selective_purgeCta"),
            CloseButtonText = L.S("action_cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var accent = Application.Current.Resources["AccentButtonStyle"] as Style;
        if (accent is not null) dialog.PrimaryButtonStyle = accent;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var history = App.Services.GetRequiredService<IPurgeHistoryStore>();
        try
        {
            if (pivot.SelectedIndex == 0)
            {
                var urls = SplitLines(urlInput.Text);
                if (urls.Length == 0) return new SelectivePurgeResult(false, L.S("selective_urlsEmptyError"));
                var batch = await cache.PurgeUrlsAsync(zone.Id, urls).ConfigureAwait(true);
                if (batch.IsFullSuccess)
                {
                    history.Record(new PurgeHistoryEntry(
                        DateTimeOffset.Now, PurgeKind.Urls, zone.Name, urls.Length, true, batch.FirstPurgeId, null));
                    var successMsg = urls.Length == 1
                        ? L.Format("selective_successUrlOne", batch.FirstPurgeId ?? string.Empty)
                        : L.Format("selective_successUrlsFmt", urls.Length, batch.FirstPurgeId ?? string.Empty);
                    return new SelectivePurgeResult(true, successMsg);
                }
                var err = batch.FirstFailure?.UserMessage ?? L.S("selective_partialDefaultErr");
                history.Record(new PurgeHistoryEntry(
                    DateTimeOffset.Now, PurgeKind.Urls, zone.Name, urls.Length, false, null, err));
                return new SelectivePurgeResult(false, L.Format("selective_partialFailFmt", batch.SuccessCount, batch.Chunks.Count) + " " + err);
            }
            else
            {
                var hosts = SplitLines(hostInput.Text);
                if (hosts.Length == 0) return new SelectivePurgeResult(false, L.S("selective_hostsEmptyError"));
                var outcome = await cache.PurgeHostsAsync(zone.Id, hosts).ConfigureAwait(true);
                history.Record(new PurgeHistoryEntry(
                    DateTimeOffset.Now, PurgeKind.Hosts, zone.Name, hosts.Length, true, outcome.Id, null));
                return new SelectivePurgeResult(true, L.Format("selective_successHostsFmt", outcome.Id));
            }
        }
        catch (CloudflareApiException ex)
        {
            history.Record(new PurgeHistoryEntry(
                DateTimeOffset.Now,
                pivot.SelectedIndex == 0 ? PurgeKind.Urls : PurgeKind.Hosts,
                zone.Name, 0, false, null, ex.Error.UserMessage));
            return new SelectivePurgeResult(false, ex.Error.UserMessage);
        }
    }

    public static async Task ShowHistoryAsync(XamlRoot root)
    {
        var store = App.Services.GetRequiredService<IPurgeHistoryStore>();
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MinWidth = 540,
            MaxHeight = 380,
        };
        PopulateHistoryList(list, store);
        store.Changed += (_, _) =>
        {
            root.Content.DispatcherQueue?.TryEnqueue(() => PopulateHistoryList(list, store));
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = L.S("history_dialogTitle"),
            Content = list,
            PrimaryButtonText = L.S("history_clearCta"),
            CloseButtonText = L.S("action_done"),
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            store.Clear();
        };
        await dialog.ShowAsync();
    }

    private static void PopulateHistoryList(ListView list, IPurgeHistoryStore store)
    {
        list.Items.Clear();
        var entries = store.GetAll();
        if (entries.Count == 0)
        {
            list.Items.Add(new TextBlock
            {
                Text = L.S("history_sessionEmpty"),
                Style = TryStyle("FPCaptionStyle"),
                Margin = new Thickness(12),
            });
            return;
        }

        foreach (var entry in entries)
        {
            var row = new Grid { Padding = new Thickness(8, 6, 8, 6), ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var statusDot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 8, Height = 8,
                Fill = entry.Success
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3E, 0xC4, 0x6F))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(statusDot, 0);

            var stack = new StackPanel { Spacing = 2 };
            var title = new TextBlock
            {
                Text = DescribeEntry(entry),
                Style = TryStyle("FPBodyStyle"),
            };
            var caption = new TextBlock
            {
                Text = entry.Success
                    ? (entry.PurgeId is null ? "OK" : $"ID: {entry.PurgeId}")
                    : (entry.ErrorMessage ?? "error"),
                Style = TryStyle("FPCaptionStyle"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
            };
            stack.Children.Add(title);
            stack.Children.Add(caption);
            Grid.SetColumn(stack, 1);

            var time = new TextBlock
            {
                Text = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture),
                Style = TryStyle("FPMonoSmallStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(time, 2);

            row.Children.Add(statusDot);
            row.Children.Add(stack);
            row.Children.Add(time);
            list.Items.Insert(0, row); // newest first
        }
    }

    private static string DescribeEntry(PurgeHistoryEntry e) => e.Kind switch
    {
        PurgeKind.Everything => L.Format("history_entryEverythingFmt", e.ZoneOrAccount),
        PurgeKind.Urls => e.Count == 1
            ? L.Format("history_entryUrlOneFmt", e.ZoneOrAccount)
            : L.Format("history_entryUrlsFmt", e.Count, e.ZoneOrAccount),
        PurgeKind.Hosts => e.Count == 1
            ? L.Format("history_entryHostOneFmt", e.ZoneOrAccount)
            : L.Format("history_entryHostsFmt", e.Count, e.ZoneOrAccount),
        PurgeKind.BulkFavorites => L.Format("history_entryBulkFavsFmt", e.Count),
        PurgeKind.BulkAccount => L.Format("history_entryBulkAccountFmt", e.ZoneOrAccount, e.Count),
        _ => e.ZoneOrAccount,
    };

    private static string[] SplitLines(string? input)
        => string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Brush? ThemeBrush(XamlRoot root, string key)
    {
        var theme = root.Content is FrameworkElement fe
            ? (fe.ActualTheme == ElementTheme.Dark ? "Dark" : "Light")
            : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(theme, out var dict)
            && dict is ResourceDictionary rd
            && rd.TryGetValue(key, out var value))
        {
            return value as Brush;
        }
        return null;
    }

    private static Style? TryStyle(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value))
            return value as Style;
        return null;
    }
}

internal sealed record SelectivePurgeResult(bool Success, string Message);

internal sealed class SettingsDialogResult
{
    public bool AddAccount { get; set; }
    public bool AccountsChanged { get; set; }
}
