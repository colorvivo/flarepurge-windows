using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlarePurge.App.Localization;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;
using FlarePurge.Core.Purge;
using FlarePurge.Core.Services;

namespace FlarePurge.App.ViewModels;

public sealed partial class ZoneListViewModel : ObservableObject
{
    private readonly IZoneService _zones;
    private readonly ICacheService _cache;
    private readonly IKeychainProvider _keychain;
    private readonly IAccountStore _store;
    private readonly IPurgeHistoryStore _history;
    private readonly IZoneCacheStore _zoneCache;

    public static string AllAccountsFilter => L.S("allAccounts_filter");

    private readonly List<ZoneDisplayItem> _allZones = new();

    public ObservableCollection<ZoneDisplayItem> Zones { get; } = new();
    public ObservableCollection<ZoneGroup> ZonesGrouped { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(HasZones))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSearchEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowRetry))]
    [NotifyPropertyChangedFor(nameof(ShowReauth))]
    [NotifyPropertyChangedFor(nameof(ShowSearchBox))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZones))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSearchEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowSearchBox))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRetry))]
    [NotifyPropertyChangedFor(nameof(ShowReauth))]
    private bool _isSessionInvalid;

    [ObservableProperty]
    private string _accountLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSearchEmpty))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedAccountFilter = L.S("allAccounts_filter");

    [ObservableProperty]
    private IReadOnlyList<string> _availableAccountFilters = new[] { L.S("allAccounts_filter") };

    [ObservableProperty]
    private bool _showAccountFilter;

    [ObservableProperty]
    private bool _useGroupedView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastUpdated))]
    private DateTimeOffset? _lastUpdatedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdatedLabel))]
    private bool _isFromCache;

    public bool HasLastUpdated => LastUpdatedAt.HasValue;

    public string LastUpdatedLabel
    {
        get
        {
            if (LastUpdatedAt is not { } t) return string.Empty;
            return L.Format("zones_updatedRelative", RelativeTime(t));
        }
    }

    private static string RelativeTime(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when.ToUniversalTime();
        if (delta.TotalSeconds < 30) return L.S("time_justNow");
        if (delta.TotalMinutes < 1) return L.Format("time_secondsAgo", (int)delta.TotalSeconds);
        if (delta.TotalMinutes < 60) return L.Format("time_minutesAgo", (int)delta.TotalMinutes);
        if (delta.TotalHours < 24) return L.Format("time_hoursAgo", (int)delta.TotalHours);
        return L.Format("time_daysAgo", (int)delta.TotalDays);
    }

    public bool HasError => !string.IsNullOrEmpty(StatusMessage);
    public bool HasZones => !IsLoading && Zones.Count > 0;
    public bool IsEmpty =>
        !IsLoading && !HasError && _allZones.Count == 0 && string.IsNullOrEmpty(SearchText);
    public bool IsSearchEmpty =>
        !IsLoading && !HasError && _allZones.Count > 0 && Zones.Count == 0 && !string.IsNullOrEmpty(SearchText);
    public bool ShowSearchBox => !IsLoading && !HasError && _allZones.Count > 0;
    public bool ShowRetry => HasError && !IsSessionInvalid;
    public bool ShowReauth => HasError && IsSessionInvalid;
    public bool HasFavorites => _allZones.Any(z => z.IsFavorite);
    public int FavoriteCount => _allZones.Count(z => z.IsFavorite);

    public bool HasAccountFilterActive =>
        ShowAccountFilter
        && !string.IsNullOrEmpty(SelectedAccountFilter)
        && !string.Equals(SelectedAccountFilter, AllAccountsFilter, StringComparison.Ordinal);

    public int AccountFilterZoneCount => HasAccountFilterActive
        ? CountZonesInCfAccount(SelectedAccountFilter)
        : 0;

    public event EventHandler? SignedOut;
    public event EventHandler<ZoneDisplayItem>? ZoneSelected;
    public event EventHandler? AboutRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? AddAccountRequested;

    public IReadOnlyList<StoredAccount> Accounts => _store.LoadAccounts();
    public string? ActiveAccountId => _store.GetActiveAccountId();

    public ZoneListViewModel(IZoneService zones, ICacheService cache, IKeychainProvider keychain, IAccountStore store, IPurgeHistoryStore history, IZoneCacheStore zoneCache)
    {
        _zones = zones;
        _cache = cache;
        _keychain = keychain;
        _store = store;
        _history = history;
        _zoneCache = zoneCache;

        RefreshActiveAccountLabel();
    }

    private void RefreshActiveAccountLabel()
    {
        var activeId = _store.GetActiveAccountId();
        if (activeId is null) { AccountLabel = string.Empty; return; }
        var account = _store.LoadAccounts().FirstOrDefault(a => a.Id == activeId);
        AccountLabel = account?.Label ?? "Cloudflare";
    }

    public async Task SwitchAccountAsync(StoredAccount account)
    {
        if (account is null) return;
        if (account.Id == _store.GetActiveAccountId()) return;
        _store.SetActiveAccountId(account.Id);
        RefreshActiveAccountLabel();
        await LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    public void RequestAddAccount() => AddAccountRequested?.Invoke(this, EventArgs.Empty);

    public void SelectZone(ZoneDisplayItem zone) => ZoneSelected?.Invoke(this, zone);

    public ZoneDisplayItem? GetFavoriteByIndex(int oneBasedIndex)
    {
        if (oneBasedIndex < 1) return null;
        return _allZones
            .Where(z => z.IsFavorite)
            .OrderBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(oneBasedIndex - 1)
            .FirstOrDefault();
    }

    public Task<PurgeOutcome> PurgeEverythingAsync(string zoneId, CancellationToken ct = default)
    {
        // Resolve the display name here, on the caller's thread. The bulk path
        // must NOT re-read _allZones from its parallel workers (see PurgeManyAsync).
        var zoneName = _allZones.FirstOrDefault(z => z.Id == zoneId)?.Name ?? zoneId;
        return PurgeZoneAsync(zoneId, zoneName, ct);
    }

    private async Task<PurgeOutcome> PurgeZoneAsync(string zoneId, string zoneName, CancellationToken ct = default)
    {
        try
        {
            var result = await _cache.PurgeEverythingAsync(zoneId, ct).ConfigureAwait(true);
            _history.Record(new PurgeHistoryEntry(
                DateTimeOffset.Now, PurgeKind.Everything, zoneName, 1, true, result.Id, null));
            return PurgeOutcome.Ok(result.Id);
        }
        catch (CloudflareApiException ex)
        {
            // A cancelled request surfaces as CloudflareApiError.Cancelled (mapped in
            // ApiClient), so C2 cancellation flows through this typed path too.
            _history.Record(new PurgeHistoryEntry(
                DateTimeOffset.Now, PurgeKind.Everything, zoneName, 1, false, null, ex.Error.UserMessage));
            return PurgeOutcome.Failure(ex.Error.UserMessage);
        }
    }

    public async Task<BulkPurgeSummary> PurgeAllFavoritesAsync(CancellationToken ct = default)
    {
        var summary = await PurgeManyAsync(_allZones.Where(z => z.IsFavorite).ToArray(), ct).ConfigureAwait(true);
        RecordBulkSummary(PurgeKind.BulkFavorites, L.S("history_bulkFavsLabel"), summary);
        return summary;
    }

    public async Task<BulkPurgeSummary> PurgeAllInCfAccountAsync(string cfAccountName, CancellationToken ct = default)
    {
        var zones = _allZones
            .Where(z => string.Equals(z.AccountName, cfAccountName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var summary = await PurgeManyAsync(zones, ct).ConfigureAwait(true);
        RecordBulkSummary(PurgeKind.BulkAccount, cfAccountName, summary);
        return summary;
    }

    private void RecordBulkSummary(PurgeKind kind, string label, BulkPurgeSummary summary)
    {
        if (summary.Total == 0) return;
        var errorMessage = summary.IsFullSuccess
            ? null
            : L.Format("bulk_summaryFailuresFmt", summary.FailureCount, summary.Total);
        _history.Record(new PurgeHistoryEntry(
            DateTimeOffset.Now, kind, label, summary.Total, summary.IsFullSuccess, null, errorMessage));
    }

    public int CountZonesInCfAccount(string cfAccountName)
        => _allZones.Count(z => string.Equals(z.AccountName, cfAccountName, StringComparison.OrdinalIgnoreCase));

    private async Task<BulkPurgeSummary> PurgeManyAsync(IReadOnlyCollection<ZoneDisplayItem> zones, CancellationToken ct = default)
    {
        if (zones.Count == 0) return BulkPurgeSummary.Empty;

        var outcomes = new ConcurrentBag<(string Name, PurgeOutcome Outcome)>();
        try
        {
            await Parallel.ForEachAsync(
                zones,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
                async (zone, token) =>
                {
                    // Use the snapshot's name; never read _allZones here (the UI thread
                    // may be mutating it via a concurrent silent refresh → crash).
                    var outcome = await PurgeZoneAsync(zone.Id, zone.Name, token).ConfigureAwait(false);
                    outcomes.Add((zone.Name, outcome));
                }).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // C2: the user cancelled. Summarise whatever completed; zones never
            // scheduled simply aren't counted.
        }

        var ordered = outcomes.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var success = ordered.Count(o => o.Outcome.Success);
        var failures = ordered
            .Where(o => !o.Outcome.Success)
            .Select(o => (o.Name, o.Outcome.Message))
            .ToArray();
        return new BulkPurgeSummary(ordered.Length, success, failures);
    }

    [RelayCommand]
    private Task LoadAsync() => LoadInternalAsync(forceNetwork: false);

    [RelayCommand]
    private Task RefreshAsync() => LoadInternalAsync(forceNetwork: true);

    private async Task LoadInternalAsync(bool forceNetwork)
    {
        var activeId = _store.GetActiveAccountId();
        var cached = !forceNetwork && activeId is not null ? _zoneCache.Get(activeId) : null;

        StatusMessage = string.Empty;
        IsSessionInvalid = false;

        if (cached is not null)
        {
            // Hydrate instantly from cache, then refresh silently in the background.
            ApplyZones(cached.Zones.Select(c => c.ToZone()).ToArray());
            LastUpdatedAt = cached.FetchedAt;
            IsFromCache = true;
            OnPropertyChanged(nameof(LastUpdatedLabel));

            _ = FetchAndApplyAsync(activeId, silent: true);
            return;
        }

        IsLoading = true;
        _allZones.Clear();
        Zones.Clear();
        ZonesGrouped.Clear();
        SelectedAccountFilter = AllAccountsFilter;
        AvailableAccountFilters = new[] { AllAccountsFilter };
        ShowAccountFilter = false;
        UseGroupedView = false;
        OnPropertyChanged(nameof(HasZones));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(ShowSearchBox));

        await FetchAndApplyAsync(activeId, silent: false).ConfigureAwait(true);
    }

    private async Task FetchAndApplyAsync(string? activeId, bool silent)
    {
        try
        {
            var zones = await _zones.FetchAllZonesAsync().ConfigureAwait(true);

            // The active account may have changed while this fetch was in flight.
            // The Bearer token is resolved per-request from whatever account is
            // active NOW, so these zones may belong to a different account than the
            // one we started for. If so, drop the result — never paint it or, worse,
            // cache it under `activeId` (which would poison that account's cache with
            // another account's zones). The now-active account's own load repaints.
            if (activeId != _store.GetActiveAccountId()) return;

            ApplyZones(zones);
            LastUpdatedAt = DateTimeOffset.UtcNow;
            IsFromCache = false;
            OnPropertyChanged(nameof(LastUpdatedLabel));
            if (activeId is not null) _zoneCache.Save(activeId, zones);
        }
        catch (CloudflareApiException ex)
        {
            if (silent)
            {
                // Paridad Apple: conservar el cache visible para todo error (incluso
                // 401/403) y dejar que el usuario pida refresh manual si sospecha algo.
                // Swallow; no state change. `ErrorDisplay.log` equivalent: we trace
                // to the crash log sink but don't touch the UI.
                System.Diagnostics.Debug.WriteLine($"[ZoneList] silent refresh error (swallowed): {ex.Error.UserMessage}");
            }
            else
            {
                StatusMessage = ex.Error.UserMessage;
                IsSessionInvalid = ex.Error is CloudflareApiError.Unauthorized or CloudflareApiError.Forbidden;
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasZones));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(ShowSearchBox));
            OnPropertyChanged(nameof(HasFavorites));
            OnPropertyChanged(nameof(FavoriteCount));
        }
    }

    private void ApplyZones(IReadOnlyList<Zone> zones)
    {
        // Paridad Apple: saltar la reasignación cuando el payload entrante es
        // equivalente al actual evita recrear ListView items en cada silent
        // refresh (que dispara selection-lost + flicker + pierde scroll).
        if (ZoneContentEquals(_allZones, zones)) return;

        var favoriteIds = _store.GetFavorites().Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        _allZones.Clear();
        foreach (var zone in zones)
            _allZones.Add(ZoneDisplayItem.FromZone(zone, favoriteIds.Contains(zone.Id)));

        var cfAccounts = _allZones
            .Select(z => z.AccountName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        UseGroupedView = cfAccounts.Length > 1;
        ShowAccountFilter = cfAccounts.Length > 1;
        AvailableAccountFilters = new[] { AllAccountsFilter }.Concat(cfAccounts).ToArray();

        RefreshFilter();
    }

    private static bool ZoneContentEquals(IReadOnlyList<ZoneDisplayItem> current, IReadOnlyList<Zone> incoming)
    {
        if (current.Count != incoming.Count) return false;

        // Compare ordered by Id so the check is stable against server ordering.
        var currentById = current.OrderBy(z => z.Id, StringComparer.Ordinal).ToArray();
        var incomingById = incoming.OrderBy(z => z.Id, StringComparer.Ordinal).ToArray();

        for (int i = 0; i < currentById.Length; i++)
        {
            var a = currentById[i];
            var b = incomingById[i];
            if (!string.Equals(a.Id, b.Id, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Status, b.Status, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Plan, b.PlanName, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.AccountName, b.AccountName, StringComparison.Ordinal)) return false;
            if (a.CreatedOn != b.CreatedOn) return false;
            if (!NameServersEqual(a.NameServers, b.NameServers)) return false;
        }
        return true;
    }

    private static bool NameServersEqual(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
        return true;
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void OpenAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ToggleFavorite(ZoneDisplayItem? zone)
    {
        if (zone is null) return;
        zone.IsFavorite = !zone.IsFavorite;
        PersistFavorites();
        RefreshFilter();
    }

    public void PersistAfterToggle(ZoneDisplayItem zone)
    {
        PersistFavorites();
        RefreshFilter();
        OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(FavoriteCount));
    }

    [RelayCommand]
    private void SignOut()
    {
        var activeId = _store.GetActiveAccountId();
        var accounts = _store.LoadAccounts();

        if (activeId is not null)
        {
            var current = accounts.FirstOrDefault(a => a.Id == activeId);
            if (current is not null)
                _keychain.Delete(current.TokenKeychainAccount);
            _zoneCache.Delete(activeId);
        }

        var remaining = accounts.Where(a => a.Id != activeId).ToArray();
        _store.SaveAccounts(remaining);
        // If another account is saved, activate it so the user lands back in the list; otherwise go to the wizard.
        _store.SetActiveAccountId(remaining.Length > 0 ? remaining[0].Id : null);

        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    partial void OnSelectedAccountFilterChanged(string value)
    {
        RefreshFilter();
        OnPropertyChanged(nameof(HasAccountFilterActive));
        OnPropertyChanged(nameof(AccountFilterZoneCount));
    }

    private void PersistFavorites()
    {
        // Favorites belong to a single GLOBAL list, but _allZones only holds the
        // ACTIVE account's zones. FavoritesMerge preserves the favorites of every
        // other account (audit C1) — it lives in Core so it's unit-tested there.
        var visible = _allZones
            .Select(z => new VisibleZone(z.Id, z.Name, z.IsFavorite))
            .ToArray();
        // _allZones are the active account's zones, so it owns any newly-favorited
        // zone (audit C4: lets the tray purge with the right token).
        var ownerId = _store.GetActiveAccountId();
        _store.SaveFavorites(FavoritesMerge.Merge(_store.GetFavorites(), visible, ownerId, DateTimeOffset.UtcNow));
    }

    private void RefreshFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var accountFilter = SelectedAccountFilter ?? AllAccountsFilter;

        IEnumerable<ZoneDisplayItem> filtered = _allZones;
        if (!string.Equals(accountFilter, AllAccountsFilter, StringComparison.Ordinal))
        {
            filtered = filtered.Where(z => string.Equals(z.AccountName, accountFilter, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Length > 0)
        {
            filtered = filtered.Where(z => z.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var sorted = filtered
            .OrderByDescending(z => z.IsFavorite)
            .ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Zones.Clear();
        foreach (var z in sorted) Zones.Add(z);

        ZonesGrouped.Clear();
        foreach (var bucket in sorted
                     .GroupBy(z => z.AccountName ?? "Cloudflare", StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var group = new ZoneGroup(bucket.Key);
            foreach (var zone in bucket) group.Add(zone);
            ZonesGrouped.Add(group);
        }

        OnPropertyChanged(nameof(HasZones));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
    }
}

public sealed class ZoneGroup : ObservableCollection<ZoneDisplayItem>
{
    public string Key { get; }

    public ZoneGroup(string key) => Key = key;
}

public sealed partial class ZoneDisplayItem : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string Status { get; }
    public string? Plan { get; }
    public string? AccountName { get; }
    public IReadOnlyList<string>? NameServers { get; }
    public DateTimeOffset? CreatedOn { get; }

    [ObservableProperty]
    private bool _isFavorite;

    public ZoneDisplayItem(
        string id,
        string name,
        string status,
        string? plan,
        string? accountName,
        IReadOnlyList<string>? nameServers = null,
        DateTimeOffset? createdOn = null,
        bool isFavorite = false)
    {
        Id = id;
        Name = name;
        Status = status;
        Plan = plan;
        AccountName = accountName;
        NameServers = nameServers;
        CreatedOn = createdOn;
        _isFavorite = isFavorite;
    }

    public bool IsActive => string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);

    public string StatusDisplay => Status.Length == 0
        ? Status
        : char.ToUpper(Status[0], CultureInfo.InvariantCulture) + Status[1..];

    public string Meta => string.Join(" · ", new[] { Plan, AccountName }
        .Where(s => !string.IsNullOrEmpty(s))!);

    public string AccountDisplay => string.IsNullOrEmpty(AccountName) ? "—" : AccountName;
    public string PlanDisplay => string.IsNullOrEmpty(Plan) ? "—" : Plan;

    public string NameServersDisplay => NameServers is { Count: > 0 } ns
        ? string.Join(Environment.NewLine, ns)
        : "—";

    public string CreatedOnDisplay => CreatedOn is { } date
        ? date.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
        : "—";

    public static ZoneDisplayItem FromZone(Zone zone, bool isFavorite = false)
        => new(
            zone.Id,
            zone.Name,
            zone.Status,
            zone.PlanName,
            zone.AccountName,
            zone.NameServers,
            zone.CreatedOn,
            isFavorite);
}

public sealed record PurgeOutcome(bool Success, string Message)
{
    public static PurgeOutcome Ok(string purgeId) => new(true, L.Format("purge_queuedIdFmt", purgeId));
    public static PurgeOutcome Failure(string message) => new(false, message);
}

public sealed record BulkPurgeSummary(
    int Total,
    int SuccessCount,
    IReadOnlyList<(string Name, string Message)> Failures)
{
    public static readonly BulkPurgeSummary Empty = new(0, 0, Array.Empty<(string, string)>());

    public int FailureCount => Total - SuccessCount;
    public bool IsFullSuccess => Total > 0 && FailureCount == 0;
}
