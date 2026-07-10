using System;
using System.Threading.Tasks;
using FlarePurge.App.Localization;
using FlarePurge.Core.Api;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlarePurge.App;

/// <summary>
/// Runs an async event-handler body under a top-level catch so an exception can
/// never ride an <c>async void</c> up to the process <c>UnhandledException</c>
/// and terminate the app (audit X1). Views only caught
/// <see cref="CloudflareApiException"/>; anything else (a concurrent
/// ContentDialog COMException, an IOException from a store, a vault limit) killed
/// the process. Here the typed API error keeps its localized message and any
/// other exception is logged (sanitized, via <see cref="App"/>) and surfaced as a
/// generic dialog. The dialog itself is guarded so this last-resort path can't
/// re-throw (e.g. when a ContentDialog is already open).
/// </summary>
internal static class Safe
{
    public static async void Fire(XamlRoot? xamlRoot, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (CloudflareApiException ex)
        {
            await ShowErrorAsync(xamlRoot, L.S("error_unexpected_title"), ex.Error.UserMessage).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.LogHandledException(ex, "async-void");
            await ShowErrorAsync(xamlRoot, L.S("error_unexpected_title"), L.S("error_unexpected_body")).ConfigureAwait(true);
        }
    }

    private static async Task ShowErrorAsync(XamlRoot? xamlRoot, string title, string body)
    {
        if (xamlRoot is null) return;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = body,
                CloseButtonText = L.S("action_ok"),
            };
            await dialog.ShowAsync();
        }
        catch
        {
            // A ContentDialog may already be open (COMException) or the visual tree
            // may be tearing down. The original error is already logged; swallow so
            // the safety net itself cannot crash the process.
        }
    }
}
