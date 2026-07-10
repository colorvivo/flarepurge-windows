using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.App.Localization;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace FlarePurge.App.Views;

public sealed partial class ZoneListView : UserControl
{
    public ZoneListViewModel ViewModel { get; }

    public event EventHandler? ReauthRequested;

    public ZoneListView(ZoneListViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        RegisterExtraAccelerators();
        _ = ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void RegisterExtraAccelerators()
    {
        var comma = new KeyboardAccelerator
        {
            Key = (VirtualKey)0xBC,
            Modifiers = VirtualKeyModifiers.Control,
        };
        comma.Invoked += OnCtrlComma;
        KeyboardAccelerators.Add(comma);

        for (int i = 1; i <= 9; i++)
        {
            var accel = new KeyboardAccelerator
            {
                Key = (VirtualKey)((int)VirtualKey.Number0 + i),
                Modifiers = VirtualKeyModifiers.Control,
            };
            accel.Invoked += OnCtrlNumber;
            KeyboardAccelerators.Add(accel);
        }
    }

    public static Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility FlatListVis(bool hasZones, bool useGrouped)
        => (hasZones && !useGrouped) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility GroupedListVis(bool hasZones, bool useGrouped)
        => (hasZones && useGrouped) ? Visibility.Visible : Visibility.Collapsed;

    public static SolidColorBrush StatusBrush(bool isActive)
        => isActive
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3E, 0xC4, 0x6F))  // FPSuccess hardcoded: static x:Bind helpers can't resolve ThemeDictionaries.
            : new SolidColorBrush(Colors.Gray);

    public static string FavouritesButtonLabel(int count)
        => count == 0 ? string.Empty : L.Format("purge_favorites_cta", count);

    public static string ZoneCountLabel(int count)
        => count switch
        {
            0 => L.S("zoneList_sortedOnly"),
            1 => L.S("zoneList_zonesSortedOne"),
            _ => L.Format("zoneList_zonesSortedFmt", count),
        };

    public static string PurgeAccountLabel(int count, string? accountName)
    {
        if (count == 0 || string.IsNullOrEmpty(accountName)) return string.Empty;
        return count == 1
            ? L.Format("zoneList_purgeAccountOne", accountName)
            : L.Format("zoneList_purgeAccountFmt", count, accountName);
    }

    private void OnZoneItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ZoneDisplayItem zone) ViewModel.SelectZone(zone);
    }

    private void OnFavoriteToggled(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ZoneDisplayItem zone)
            ViewModel.PersistAfterToggle(zone);
    }

    private void OnCtrlR(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.RefreshCommand.CanExecute(null))
            _ = ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void OnCtrlF(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.ShowSearchBox)
            SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnCtrlComma(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.OpenSettingsCommand.CanExecute(null))
            ViewModel.OpenSettingsCommand.Execute(null);
    }

    private void OnCtrlNumber(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        int index = (int)sender.Key - (int)VirtualKey.Number0;
        var favorite = ViewModel.GetFavoriteByIndex(index);
        if (favorite is not null)
            ViewModel.SelectZone(favorite);
    }

    private void OnReauthClick(object sender, RoutedEventArgs e)
        => ReauthRequested?.Invoke(this, EventArgs.Empty);

    private void OnPurgeAccountClick(object sender, RoutedEventArgs e)
        => Safe.Fire(XamlRoot, async () =>
    {
        if (sender is not Button button) return;
        if (!ViewModel.HasAccountFilterActive) return;
        var accountName = ViewModel.SelectedAccountFilter;
        var count = ViewModel.AccountFilterZoneCount;
        if (count == 0) return;

        button.IsEnabled = false;
        try
        {
            var prefs = App.Services.GetRequiredService<IAccountStore>().GetPreferences();
            // Bulk operations honour the dedicated bulk-confirm preference (this
            // used to read ConfirmPurgeEverything, leaving the bulk toggle dead —
            // a user who turned off single-zone confirm could fire a whole-account
            // purge with no prompt).
            if (prefs.ConfirmBulkPurge)
            {
                var title = count == 1
                    ? L.Format("bulk_confirmAccountOne", accountName)
                    : L.Format("bulk_confirmAccountFmt", count, accountName);
                var confirmed = await AppDialogs.ShowPurgeConfirmAsync(
                    XamlRoot,
                    title,
                    L.S("bulk_confirmAccountBody"),
                    L.S("bulk_confirmAccountCta")).ConfigureAwait(true);
                if (!confirmed) return;
            }

            var progressLabel = count == 1
                ? L.Format("bulk_progressAccountFmt", 1, accountName)
                : L.Format("bulk_progressAccountFmt", count, accountName);
            using var cts = new CancellationTokenSource();
            var progress = StartBulkProgressDialog(count, cts, progressLabel);
            _ = progress.ShowAsync();
            var summary = await ViewModel.PurgeAllInCfAccountAsync(accountName, cts.Token).ConfigureAwait(true);
            progress.Hide();

            await ShowBulkOutcomeAsync(summary).ConfigureAwait(true);
        }
        finally
        {
            button.IsEnabled = true;
        }
    });

    private void OnPurgeFavoritesClick(object sender, RoutedEventArgs e)
        => Safe.Fire(XamlRoot, async () =>
    {
        if (sender is not Button button) return;
        button.IsEnabled = false;
        try
        {
            var count = ViewModel.FavoriteCount;
            if (count == 0) return;

            var prefs = App.Services.GetRequiredService<IAccountStore>().GetPreferences();
            // Bulk favourites purge honours the bulk-confirm preference (see note
            // in OnPurgeAccountClick).
            if (prefs.ConfirmBulkPurge)
            {
                var title = count == 1
                    ? L.S("bulk_confirmFavOne")
                    : L.Format("bulk_confirmFavsFmt", count);
                var confirmed = await AppDialogs.ShowPurgeConfirmAsync(
                    XamlRoot,
                    title,
                    L.S("bulk_confirmFavsBody"),
                    L.S("bulk_confirmFavsCta")).ConfigureAwait(true);
                if (!confirmed) return;
            }

            using var cts = new CancellationTokenSource();
            var progress = StartBulkProgressDialog(count, cts);
            _ = progress.ShowAsync();
            var summary = await ViewModel.PurgeAllFavoritesAsync(cts.Token).ConfigureAwait(true);
            progress.Hide();

            await ShowBulkOutcomeAsync(summary).ConfigureAwait(true);
        }
        finally
        {
            button.IsEnabled = true;
        }
    });

    private ContentDialog StartBulkProgressDialog(int count, CancellationTokenSource cts, string? labelOverride = null)
    {
        var label = labelOverride ?? (count == 1
            ? L.S("bulk_progressFavOne")
            : L.Format("bulk_progressFavsFmt", count));
        var stack = new StackPanel { Spacing = 12, Padding = new Thickness(8) };
        stack.Children.Add(new ProgressRing { IsActive = true, Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.S("bulk_dialogTitle"),
            Content = stack,
            // C2: a Cancel button so a bulk purge stuck on 429 + Retry-After (up to
            // 60s × 4 per zone) isn't a trap. Cancels the in-flight requests; the
            // summary then reports whatever completed.
            CloseButtonText = L.S("action_cancel"),
        };
        dialog.CloseButtonClick += (_, _) => cts.Cancel();
        return dialog;
    }

    private async Task ShowBulkOutcomeAsync(BulkPurgeSummary summary)
    {
        string title;
        string content;
        if (summary.IsFullSuccess)
        {
            title = summary.SuccessCount == 1
                ? L.S("bulk_summaryFullOne")
                : L.Format("bulk_summaryFullFmt", summary.SuccessCount);
            content = L.S("bulk_summaryFullBody");
        }
        else
        {
            title = L.Format("bulk_summaryPartialFmt", summary.SuccessCount, summary.Total);
            var lines = summary.Failures.Take(5).Select(f => $"• {f.Name}: {f.Message}");
            content = L.S("bulk_summaryErrorsHeader") + "\n" + string.Join("\n", lines);
            if (summary.Failures.Count > 5)
                content += "\n" + L.Format("bulk_summaryMoreErrorsFmt", summary.Failures.Count - 5);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = L.S("action_ok"),
        };
        await dialog.ShowAsync();
    }

    private async Task PurgeZoneEverythingAsync(ZoneDisplayItem zone)
    {
        var prefs = App.Services.GetRequiredService<IAccountStore>().GetPreferences();
        if (prefs.ConfirmPurgeEverything)
        {
            var confirmed = await AppDialogs.ShowPurgeConfirmAsync(
                XamlRoot,
                L.Format("purgeConfirm_titleFmt", zone.Name),
                L.S("purgeConfirm_bodyLong"),
                L.S("mac_purge_cta_all")).ConfigureAwait(true);
            if (!confirmed) return;
        }

        var progress = StartProgressDialog(zone);
        _ = progress.ShowAsync();
        var outcome = await ViewModel.PurgeEverythingAsync(zone.Id).ConfigureAwait(true);
        progress.Hide();

        await ShowOutcomeDialogAsync(zone, outcome).ConfigureAwait(true);
    }

    private void OnContextPurgeClick(object sender, RoutedEventArgs e)
        => Safe.Fire(XamlRoot, async () =>
        {
            if (sender is FrameworkElement fe && fe.Tag is ZoneDisplayItem zone)
                await PurgeZoneEverythingAsync(zone).ConfigureAwait(true);
        });

    private void OnContextOpenDetailClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ZoneDisplayItem zone)
            ViewModel.SelectZone(zone);
    }

    private void OnContextFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ZoneDisplayItem zone)
            ViewModel.PersistAfterToggle(zone);
    }

    private void OnContextCopyIdClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not ZoneDisplayItem zone) return;
        var package = new DataPackage();
        package.SetText(zone.Id);
        Clipboard.SetContent(package);
    }

    private ContentDialog StartProgressDialog(ZoneDisplayItem zone)
    {
        var stack = new StackPanel { Spacing = 12, Padding = new Thickness(8) };
        stack.Children.Add(new ProgressRing { IsActive = true, Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = L.Format("progress_purgingFmt", zone.Name),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L.S("progress_inProgress"),
            Content = stack,
        };
    }

    private async Task ShowOutcomeDialogAsync(ZoneDisplayItem zone, PurgeOutcome outcome)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = outcome.Success
                ? L.Format("purge_singleSuccessFmt", zone.Name)
                : L.Format("purge_singleFailedFmt", zone.Name),
            Content = outcome.Message,
            CloseButtonText = L.S("action_ok"),
        };
        await dialog.ShowAsync();
    }
}
