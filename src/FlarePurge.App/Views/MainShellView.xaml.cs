using System;
using System.Linq;
using System.Threading.Tasks;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FlarePurge.App.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly ZoneListViewModel _zoneListVm;
    private readonly IAccountStore _store;
    private readonly IKeychainProvider _keychain;

    private ZoneListView? _zoneListView;
    private ZoneDisplayItem? _selectedZone;

    public event EventHandler? AddAccountRequested;
    public event EventHandler? SignedOutOfAll;

    private readonly DispatcherTimer _lastUpdatedTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    public MainShellView(ZoneListViewModel zoneListVm, IAccountStore store, IKeychainProvider keychain)
    {
        _zoneListVm = zoneListVm;
        _store = store;
        _keychain = keychain;

        InitializeComponent();

        _zoneListVm.ZoneSelected += OnZoneSelected;
        _zoneListVm.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(async () => await OpenSettingsModalAsync());
        _zoneListVm.AboutRequested += (_, _) => DispatcherQueue.TryEnqueue(async () => await OpenAboutModalAsync());
        _zoneListVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ZoneListViewModel.LastUpdatedLabel)
                || e.PropertyName == nameof(ZoneListViewModel.LastUpdatedAt)
                || e.PropertyName == nameof(ZoneListViewModel.IsLoading))
            {
                DispatcherQueue.TryEnqueue(UpdateStatusBar);
            }
        };

        _lastUpdatedTimer.Tick += (_, _) => UpdateStatusBar();
        Loaded += (_, _) => { _lastUpdatedTimer.Start(); UpdateStatusBar(); };
        Unloaded += (_, _) => _lastUpdatedTimer.Stop();

        _zoneListView = new ZoneListView(_zoneListVm);
        _zoneListView.ReauthRequested += (_, _) => AddAccountRequested?.Invoke(this, EventArgs.Empty);
        ListHost.Content = _zoneListView;

        Loaded += (_, _) => RefreshAccounts();
        UpdateRightPane();
    }

    private void UpdateStatusBar()
    {
        if (_zoneListVm.HasLastUpdated)
        {
            LastUpdatedText.Text = _zoneListVm.LastUpdatedLabel;
            LastUpdatedText.Visibility = Visibility.Visible;
        }
        else
        {
            LastUpdatedText.Visibility = Visibility.Collapsed;
        }
        RefreshZonesButton.IsEnabled = !_zoneListVm.IsLoading;
    }

    private async void OnRefreshZonesClick(object sender, RoutedEventArgs e)
    {
        await _zoneListVm.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    public void RefreshAfterStoreChange()
    {
        RefreshAccounts();
        _ = _zoneListVm.LoadCommand.ExecuteAsync(null);
        _selectedZone = null;
        UpdateRightPane();
    }

    private void RefreshAccounts()
    {
        AccountsList.Children.Clear();
        var activeId = _store.GetActiveAccountId();
        foreach (var account in _store.LoadAccounts().OrderBy(a => a.Label, StringComparer.OrdinalIgnoreCase))
        {
            AccountsList.Children.Add(BuildAccountItem(account, isActive: account.Id == activeId));
        }
    }

    private Button BuildAccountItem(StoredAccount account, bool isActive)
    {
        var indicator = new Border
        {
            Width = 3,
            Height = 28,
            Background = ThemeBrush("FPAccentBrush"),
            CornerRadius = new CornerRadius(2),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = isActive ? Visibility.Visible : Visibility.Collapsed,
        };

        var label = new TextBlock
        {
            Text = account.Label,
            Style = TryStyle("FPBodyLargeStyle"),
        };
        var preview = new TextBlock
        {
            Text = account.TokenKeychainAccount,
            Style = TryStyle("FPMonoSmallStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        };

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(label);
        textStack.Children.Add(preview);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(indicator);
        row.Children.Add(textStack);

        var btn = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 8, 8, 8),
            Margin = new Thickness(0, 1, 0, 1),
            Tag = account.Id,
            Background = isActive
                ? (Brush?)ThemeBrush("FPAccentSubtleBrush") ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };

        btn.Click += OnAccountItemClick;

        var flyout = new MenuFlyout();
        var signOut = new MenuFlyoutItem
        {
            Text = "Cerrar sesión",
            Tag = account.Id,
            Icon = new SymbolIcon(Symbol.LeaveChat),
        };
        signOut.Click += OnSignOutAccountClick;
        flyout.Items.Add(signOut);
        btn.ContextFlyout = flyout;

        return btn;
    }

    private Brush? ThemeBrush(string key)
    {
        var themeKey = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var dict)
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

    private async void OnAccountItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        if (string.Equals(id, _store.GetActiveAccountId(), StringComparison.Ordinal)) return;
        var account = _store.LoadAccounts().FirstOrDefault(a => a.Id == id);
        if (account is null) return;

        await _zoneListVm.SwitchAccountAsync(account).ConfigureAwait(true);
        _selectedZone = null;
        RefreshAccounts();
        UpdateRightPane();
    }

    private void OnSignOutAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;

        var accounts = _store.LoadAccounts();
        var target = accounts.FirstOrDefault(a => a.Id == id);
        if (target is null) return;

        _keychain.Delete(target.TokenKeychainAccount);
        var remaining = accounts.Where(a => a.Id != id).ToArray();
        _store.SaveAccounts(remaining);

        if (string.Equals(id, _store.GetActiveAccountId(), StringComparison.Ordinal))
        {
            _store.SetActiveAccountId(remaining.Length > 0 ? remaining[0].Id : null);
        }

        if (remaining.Length == 0)
        {
            SignedOutOfAll?.Invoke(this, EventArgs.Empty);
            return;
        }

        RefreshAfterStoreChange();
    }

    private void OnAddAccountClick(object sender, RoutedEventArgs e)
        => AddAccountRequested?.Invoke(this, EventArgs.Empty);

    private void OnNavZonesClick(object sender, RoutedEventArgs e)
    {
        _selectedZone = null;
        UpdateRightPane();
    }

    private async void OnNavSettingsClick(object sender, RoutedEventArgs e) => await OpenSettingsModalAsync();
    private async void OnNavAboutClick(object sender, RoutedEventArgs e) => await OpenAboutModalAsync();
    private async void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null) return;
        await AppDialogs.ShowHistoryAsync(XamlRoot).ConfigureAwait(true);
    }

    private async Task OpenSettingsModalAsync()
    {
        if (XamlRoot is null) return;
        var result = await AppDialogs.ShowSettingsAsync(XamlRoot).ConfigureAwait(true);

        if (result.AccountsChanged)
        {
            if (_store.GetActiveAccountId() is null)
            {
                SignedOutOfAll?.Invoke(this, EventArgs.Empty);
                return;
            }
            RefreshAfterStoreChange();
        }

        if (result.AddAccount)
            AddAccountRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task OpenAboutModalAsync()
    {
        if (XamlRoot is null) return;
        await AppDialogs.ShowAboutAsync(XamlRoot).ConfigureAwait(true);
    }

    private void OnZoneSelected(object? sender, ZoneDisplayItem zone)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _selectedZone = zone;
            UpdateRightPane();
        });
    }

    private void UpdateRightPane()
    {
        if (_selectedZone is null)
        {
            DetailHost.Content = null;
            EmptySelectionPanel.Visibility = Visibility.Visible;
            return;
        }

        var cache = App.Services.GetRequiredService<ICacheService>();
        var history = App.Services.GetRequiredService<FlarePurge.Core.Purge.IPurgeHistoryStore>();
        var detailVm = new ZoneDetailViewModel(_selectedZone, cache, history);
        var detailView = new ZoneDetailView(detailVm);
        detailView.BackRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            _selectedZone = null;
            UpdateRightPane();
        });
        DetailHost.Content = detailView;
        EmptySelectionPanel.Visibility = Visibility.Collapsed;
    }
}
