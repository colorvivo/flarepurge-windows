using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FlarePurge.App.Localization;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Services;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FlarePurge.App;

internal sealed class TrayIconController : IDisposable
{
    private readonly Window _window;
    private readonly IAccountStore _store;
    private readonly ICacheService _cache;
    private TaskbarIcon? _icon;
    private MenuFlyout? _menu;

    public TrayIconController(Window window, IAccountStore store, ICacheService cache)
    {
        _window = window;
        _store = store;
        _cache = cache;
    }

    public bool Initialize()
    {
        try
        {
            _menu = new MenuFlyout();
            _menu.Opening += OnMenuOpening;

            _icon = new TaskbarIcon
            {
                ToolTipText = "FlarePurge",
                IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
                LeftClickCommand = new RelayCommand(ShowWindow),
                NoLeftClickDelay = true,
                ContextFlyout = _menu,
            };
            _icon.ForceCreate();
            return true;
        }
        catch
        {
            _icon = null;
            return false;
        }
    }

    private void OnMenuOpening(object? sender, object e)
    {
        if (_menu is null) return;
        _menu.Items.Clear();

        var accounts = _store.LoadAccounts();
        var activeId = _store.GetActiveAccountId();
        if (accounts.Count >= 2)
        {
            AppendHeader(L.S("tray_accountsHeader"));
            foreach (var account in accounts.OrderBy(a => a.Label, StringComparer.OrdinalIgnoreCase))
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = account.Label,
                    IsChecked = account.Id == activeId,
                };
                var captured = account;
                item.Click += (_, _) => SwitchAccount(captured);
                _menu.Items.Add(item);
            }
            _menu.Items.Add(new MenuFlyoutSeparator());
        }

        var favorites = _store.GetFavorites();
        if (favorites.Count > 0)
        {
            AppendHeader(L.S("tray_favouritesHeader"));
            foreach (var fav in favorites.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = new MenuFlyoutItem { Text = L.Format("tray_purgeFmt", fav.Name) };
                var captured = fav;
                item.Click += async (_, _) => await PurgeFavoriteAsync(captured).ConfigureAwait(false);
                _menu.Items.Add(item);
            }
            if (favorites.Count > 1)
            {
                var allItem = new MenuFlyoutItem { Text = L.Format("tray_purgeAllFmt", favorites.Count) };
                allItem.Click += async (_, _) => await PurgeAllFavoritesAsync().ConfigureAwait(false);
                _menu.Items.Add(allItem);
            }
            _menu.Items.Add(new MenuFlyoutSeparator());
        }

        var openItem = new MenuFlyoutItem { Text = L.S("tray_openApp"), Command = new RelayCommand(ShowWindow) };
        _menu.Items.Add(openItem);
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(new MenuFlyoutItem { Text = L.S("tray_quit"), Command = new RelayCommand(Quit) });
    }

    private void AppendHeader(string label)
    {
        var header = new MenuFlyoutItem
        {
            Text = label,
            IsEnabled = false,
            FontSize = 10,
        };
        _menu?.Items.Add(header);
    }

    private void SwitchAccount(StoredAccount account)
    {
        if (string.Equals(account.Id, _store.GetActiveAccountId(), StringComparison.Ordinal)) return;
        _store.SetActiveAccountId(account.Id);

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window is MainWindow main)
                main.RefreshAfterStoreChange();
        });
    }

    // C4: a favourite is purgeable only with the token of the account that owns it,
    // but _cache uses whichever account is ACTIVE — so purging a favourite from
    // another account 403/404s. Build a client bound to the favourite's own account.
    // Legacy favourites (no recorded owner) and demo mode fall back to _cache.
    private ICacheService CacheFor(FavoriteZone fav)
    {
        if (App.IsDemoMode || string.IsNullOrEmpty(fav.AccountId)) return _cache;

        var keychain = App.Services.GetRequiredService<IKeychainProvider>();
        var http = App.Services.GetRequiredService<HttpClient>();
        var rateLimiter = App.Services.GetRequiredService<RateLimiter>();
        var api = new ApiClient(http, rateLimiter, TokenProviderFactory.FromAccountId(_store, keychain, fav.AccountId));
        return new CacheService(api);
    }

    private async Task PurgeFavoriteAsync(FavoriteZone fav)
    {
        try
        {
            var result = await CacheFor(fav).PurgeEverythingAsync(fav.Id).ConfigureAwait(false);
            ShowBalloon(L.Format("tray_balloonPurgedTitleFmt", fav.Name), L.Format("tray_balloonPurgeIdFmt", result.Id));
        }
        catch (CloudflareApiException ex)
        {
            ShowBalloon(L.Format("tray_balloonFailedTitleFmt", fav.Name), ex.Error.UserMessage);
        }
    }

    private async Task PurgeAllFavoritesAsync()
    {
        var favorites = _store.GetFavorites();
        if (favorites.Count == 0) return;

        var outcomes = new ConcurrentBag<(string Name, bool Success)>();
        await Parallel.ForEachAsync(
            favorites,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (fav, _) =>
            {
                try
                {
                    await CacheFor(fav).PurgeEverythingAsync(fav.Id).ConfigureAwait(false);
                    outcomes.Add((fav.Name, true));
                }
                catch (CloudflareApiException)
                {
                    outcomes.Add((fav.Name, false));
                }
            }).ConfigureAwait(false);

        var success = outcomes.Count(o => o.Success);
        var total = outcomes.Count;
        var message = success == total
            ? (total == 1 ? L.S("tray_balloonBulkOkOne") : L.Format("tray_balloonBulkOkFmt", total))
            : L.Format("tray_balloonPartialBodyFmt", success, total);
        ShowBalloon(L.S("tray_balloonFavsPurgedTitle"), message);
    }

    private void ShowBalloon(string title, string message)
    {
        try
        {
            // H.NotifyIcon.WinUI 2.x exposes ShowNotification; wrap in try/catch so
            // a host that can't surface a balloon (policy-restricted, etc.) stays quiet
            // rather than bubbling an exception up through the Opening handler.
            _icon?.ShowNotification(title: title, message: message);
        }
        catch
        {
            // Silent fallback: the outcome still shows up inside the app next time it opens.
        }
    }

    private void ShowWindow()
    {
        if (_window is MainWindow main) main.BringToFront();
        else _window.Activate();
    }

    private void Quit()
    {
        Dispose();
        Application.Current.Exit();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
