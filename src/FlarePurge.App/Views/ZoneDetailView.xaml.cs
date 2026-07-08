using System;
using System.Threading.Tasks;
using FlarePurge.App.Localization;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlarePurge.App.Views;

public sealed partial class ZoneDetailView : UserControl
{
    public ZoneDetailViewModel ViewModel { get; }

    public event EventHandler? BackRequested;

    public ZoneDetailView(ZoneDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public static Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static string AccountSubtitle(ZoneDisplayItem zone)
        => string.IsNullOrEmpty(zone.AccountName) ? string.Empty : L.Format("mac_zoneDetail_accountOwned", zone.AccountName);

    private void OnEscape(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.IsPurging) return;
        args.Handled = true;
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnPurgeEverythingClick(object sender, RoutedEventArgs e)
        => await PurgeEverythingAsync().ConfigureAwait(true);

    private async void OnCtrlShiftP(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.IsIdle) return;
        args.Handled = true;
        await PurgeEverythingAsync().ConfigureAwait(true);
    }

    private async Task PurgeEverythingAsync()
    {
        var prefs = App.Services.GetRequiredService<IAccountStore>().GetPreferences();
        if (prefs.ConfirmPurgeEverything)
        {
            var confirmed = await AppDialogs.ShowPurgeConfirmAsync(
                XamlRoot,
                L.Format("purgeConfirm_titleFmt", ViewModel.Zone.Name),
                L.S("purgeConfirm_bodyLong"),
                L.S("mac_purge_cta_all")).ConfigureAwait(true);
            if (!confirmed) return;
        }
        await ViewModel.PurgeEverythingCommand.ExecuteAsync(null);
    }

    private async void OnSelectivePurgeClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsIdle) return;
        var cache = App.Services.GetRequiredService<ICacheService>();
        var result = await AppDialogs.ShowSelectivePurgeAsync(XamlRoot, ViewModel.Zone, cache).ConfigureAwait(true);
        if (result is null) return;

        ViewModel.SetExternalResult(result.Message);
    }
}
