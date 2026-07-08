using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlarePurge.App.Localization;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;

namespace FlarePurge.App.ViewModels;

public sealed partial class TokenWizardViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly IKeychainProvider _keychain;
    private readonly IAccountStore _store;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private string _token = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _succeeded;

    [ObservableProperty]
    private string _successSummary = string.Empty;

    public TokenWizardViewModel(HttpClient http, IKeychainProvider keychain, IAccountStore store)
    {
        _http = http;
        _keychain = keychain;
        _store = store;
    }

    public bool IsIdle => !IsBusy && !Succeeded;
    public bool HasError => !string.IsNullOrEmpty(StatusMessage);

    public event EventHandler? TokenSaved;

    [RelayCommand]
    private async Task AddTokenAsync()
    {
        var trimmed = Token.Trim();
        if (trimmed.Length == 0) return;

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var result = await TokenVerifier.ValidateAndFetchAsync(_http, trimmed).ConfigureAwait(true);
            PersistAccount(trimmed, result);
            SuccessSummary = FormatSummary(result);
            Succeeded = true;
            TokenSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (CloudflareApiException ex)
        {
            StatusMessage = ex.Error.UserMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistAccount(string token, TokenValidationResult validation)
    {
        var localId = Guid.NewGuid().ToString("N");
        var cfAccountId = validation.Accounts.FirstOrDefault()?.Id;
        var userLabel = Label.Trim();
        var label = userLabel.Length > 0
            ? userLabel
            : (validation.Accounts.FirstOrDefault()?.Name ?? "Cloudflare");

        _keychain.Save(token, localId);

        var accounts = new List<StoredAccount>(_store.LoadAccounts())
        {
            new(localId, cfAccountId, label, localId, DateTimeOffset.UtcNow),
        };
        _store.SaveAccounts(accounts);
        _store.SetActiveAccountId(localId);
    }

    private static string FormatSummary(TokenValidationResult validation)
    {
        if (validation.Accounts.Count == 0)
            return L.S("tokenWizard_success_body");
        if (validation.Accounts.Count == 1)
            return validation.Accounts[0].Name;
        return L.Format("accountPurge_menuOne", $"{validation.Accounts.Count}");
    }
}
