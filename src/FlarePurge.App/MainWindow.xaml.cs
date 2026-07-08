using System;
using FlarePurge.App.ViewModels;
using FlarePurge.App.Views;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Status;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace FlarePurge.App;

public sealed partial class MainWindow : Window
{
    private static readonly SizeInt32 InitialSize = new() { Width = 1120, Height = 760 };
    private static readonly SizeInt32 MinimumSize = new() { Width = 880, Height = 560 };

    public MainWindowViewModel ViewModel { get; }

    private MainShellView? _shell;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        InitializeComponent();

        Title = "FlarePurge";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TrySetMicaBackdrop();

        ConfigureWindow();

        var store = App.Services.GetRequiredService<IAccountStore>();
        ApplyThemeMode(store.GetPreferences().ThemeMode);
        SettingsViewModel.ThemeChanged += ApplyThemeMode;

        if (store.GetActiveAccountId() is null)
            ShowTokenWizard();
        else
            ShowShell();

        _ = CheckKillSwitchAsync();
    }

    private async System.Threading.Tasks.Task CheckKillSwitchAsync()
    {
        try
        {
            var statusService = App.Services.GetRequiredService<IRemoteStatusService>();
            var status = await statusService.FetchAsync().ConfigureAwait(true);
            if (status.Disabled)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    RootContent.Content = new KillSwitchView(status.Message);
                });
            }
        }
        catch
        {
            // Fail-open — already handled inside the service, but defense in depth.
        }
    }

    private void ApplyThemeMode(string code)
    {
        var theme = code switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (Content is FrameworkElement fe) fe.RequestedTheme = theme;
    }

    private void TrySetMicaBackdrop()
    {
        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
                return;
            }
            if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch
        {
            // Backdrop APIs unavailable — fall back to the opaque brush on the root Grid.
        }
    }

    private void ConfigureWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _appWindow.Resize(InitialSize);
            _appWindow.Changed += OnAppWindowChanged;
            _appWindow.Closing += OnAppWindowClosing;
        }
        catch
        {
            // Windowing APIs are unavailable in some test hosts — let the
            // window render with the OS default size instead of crashing.
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            var prefs = App.Services.GetRequiredService<IAccountStore>().GetPreferences();
            if (prefs.MinimizeToTray)
            {
                args.Cancel = true;
                sender.Hide();
            }
        }
        catch
        {
            // If we can't read preferences for any reason, let the close proceed.
        }
    }

    // Called from TrayIconController when user clicks "Open FlarePurge" or left-clicks the icon.
    // AppWindow.Show() is required because Activate() alone does not un-hide a window hidden via Hide().
    public void BringToFront()
    {
        try { _appWindow?.Show(); } catch { }
        Activate();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange) return;

        var current = sender.Size;
        if (current.Width >= MinimumSize.Width && current.Height >= MinimumSize.Height) return;

        sender.Resize(new SizeInt32
        {
            Width = Math.Max(current.Width, MinimumSize.Width),
            Height = Math.Max(current.Height, MinimumSize.Height),
        });
    }

    private void ShowTokenWizard()
    {
        var vm = App.Services.GetRequiredService<TokenWizardViewModel>();
        vm.TokenSaved += OnTokenSaved;
        RootContent.Content = new TokenWizardView(vm);
    }

    private void ShowTokenWizardForAdd()
    {
        var vm = App.Services.GetRequiredService<TokenWizardViewModel>();
        vm.TokenSaved += OnTokenSavedFromAdd;
        var view = new TokenWizardView(vm) { CanCancel = true };
        view.CancelRequested += OnWizardCancelled;
        RootContent.Content = view;
    }

    private void ShowShell()
    {
        if (_shell is null)
        {
            var zoneListVm = App.Services.GetRequiredService<ZoneListViewModel>();
            var store = App.Services.GetRequiredService<IAccountStore>();
            var keychain = App.Services.GetRequiredService<IKeychainProvider>();
            _shell = new MainShellView(zoneListVm, store, keychain);
            _shell.AddAccountRequested += OnAddAccountRequested;
            _shell.SignedOutOfAll += OnSignedOutOfAll;
        }
        RootContent.Content = _shell;
    }

    private void OnTokenSaved(object? sender, EventArgs e)
    {
        if (sender is TokenWizardViewModel vm) vm.TokenSaved -= OnTokenSaved;
        DispatcherQueue.TryEnqueue(ShowShell);
    }

    private void OnSignedOutOfAll(object? sender, EventArgs e)
    {
        if (_shell is not null)
        {
            _shell.AddAccountRequested -= OnAddAccountRequested;
            _shell.SignedOutOfAll -= OnSignedOutOfAll;
        }
        _shell = null;
        DispatcherQueue.TryEnqueue(ShowTokenWizard);
    }

    private void OnAddAccountRequested(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(ShowTokenWizardForAdd);

    // Called by TrayIconController after a tray action mutates the account store.
    public void RefreshAfterStoreChange()
    {
        var store = App.Services.GetRequiredService<IAccountStore>();
        if (store.GetActiveAccountId() is null)
        {
            // last account was removed via tray — bail to wizard.
            if (_shell is not null)
            {
                _shell.AddAccountRequested -= OnAddAccountRequested;
                _shell.SignedOutOfAll -= OnSignedOutOfAll;
                _shell = null;
            }
            DispatcherQueue.TryEnqueue(ShowTokenWizard);
            return;
        }

        if (_shell is null)
        {
            DispatcherQueue.TryEnqueue(ShowShell);
            return;
        }

        DispatcherQueue.TryEnqueue(_shell.RefreshAfterStoreChange);
    }

    private void OnTokenSavedFromAdd(object? sender, EventArgs e)
    {
        if (sender is TokenWizardViewModel vm) vm.TokenSaved -= OnTokenSavedFromAdd;
        // Account list changed: refresh the shell so it picks up the new active account.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_shell is null)
                ShowShell();
            else
            {
                RootContent.Content = _shell;
                _shell.RefreshAfterStoreChange();
            }
        });
    }

    private void OnWizardCancelled(object? sender, EventArgs e)
    {
        if (sender is TokenWizardView view)
            view.CancelRequested -= OnWizardCancelled;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_shell is null) ShowShell();
            else RootContent.Content = _shell;
        });
    }
}
