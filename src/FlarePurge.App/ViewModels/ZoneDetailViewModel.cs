using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlarePurge.App.Localization;
using FlarePurge.Core.Api;
using FlarePurge.Core.Purge;
using FlarePurge.Core.Services;

namespace FlarePurge.App.ViewModels;

public sealed partial class ZoneDetailViewModel : ObservableObject
{
    private readonly ICacheService _cache;
    private readonly IPurgeHistoryStore _history;

    public ZoneDisplayItem Zone { get; }

    [ObservableProperty]
    private string _urlsInput = string.Empty;

    [ObservableProperty]
    private string _hostsInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isPurging;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private string _lastResult = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLastResultError))]
    [NotifyPropertyChangedFor(nameof(IsLastResultSuccess))]
    private bool _lastResultSuccess;

    public ZoneDetailViewModel(ZoneDisplayItem zone, ICacheService cache, IPurgeHistoryStore history)
    {
        Zone = zone;
        _cache = cache;
        _history = history;
    }

    public bool IsIdle => !IsPurging;
    public bool HasResult => !string.IsNullOrEmpty(LastResult);
    public bool IsLastResultSuccess => HasResult && LastResultSuccess;
    public bool IsLastResultError => HasResult && !LastResultSuccess;

    public void SetExternalResult(string message, bool success = true)
    {
        LastResultSuccess = success;
        LastResult = message;
    }

    [RelayCommand]
    private async Task PurgeEverythingAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _cache.PurgeEverythingAsync(Zone.Id).ConfigureAwait(true);
            _history.Record(new PurgeHistoryEntry(
                DateTimeOffset.Now, PurgeKind.Everything, Zone.Name, 1, true, result.Id, null));
            Success(L.Format("purge_queuedIdFmt", result.Id));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PurgeUrlsAsync()
    {
        var urls = ParseLines(UrlsInput);
        if (urls.Count == 0) { Fail(L.S("selective_urlsEmptyError")); return; }

        await RunAsync(async () =>
        {
            var batch = await _cache.PurgeUrlsAsync(Zone.Id, urls).ConfigureAwait(true);
            if (batch.IsFullSuccess)
            {
                Success(urls.Count == 1
                    ? L.Format("selective_successUrlOne", batch.FirstPurgeId ?? string.Empty)
                    : L.Format("selective_successUrlsFmt", urls.Count, batch.FirstPurgeId ?? string.Empty));
            }
            else
            {
                var err = batch.FirstFailure?.UserMessage ?? L.S("selective_partialDefaultErr");
                Fail(L.Format("selective_partialFailFmt", batch.SuccessCount, batch.Chunks.Count, err));
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PurgeHostsAsync()
    {
        var hosts = ParseLines(HostsInput);
        if (hosts.Count == 0) { Fail(L.S("selective_hostsEmptyError")); return; }

        await RunAsync(async () =>
        {
            var result = await _cache.PurgeHostsAsync(Zone.Id, hosts).ConfigureAwait(true);
            Success(L.Format("selective_successHostsFmt", result.Id));
        }).ConfigureAwait(true);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsPurging = true;
        LastResult = string.Empty;
        try { await action().ConfigureAwait(true); }
        catch (CloudflareApiException ex)
        {
            _history.Record(new PurgeHistoryEntry(
                DateTimeOffset.Now, PurgeKind.Everything, Zone.Name, 1, false, null, ex.Error.UserMessage));
            Fail(ex.Error.UserMessage);
        }
        finally { IsPurging = false; }
    }

    private void Success(string message)
    {
        LastResultSuccess = true;
        LastResult = message;
    }

    private void Fail(string message)
    {
        LastResultSuccess = false;
        LastResult = message;
    }

    private static IReadOnlyList<string> ParseLines(string raw) =>
        raw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
           .Select(l => l.Trim())
           .Where(l => l.Length > 0)
           .ToArray();
}
