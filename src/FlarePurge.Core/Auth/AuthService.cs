using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Auth;

public sealed class AuthService(IApiClient client) : IAuthService
{
    private readonly IApiClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<TokenVerification> VerifyAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync<TokenVerification>(Endpoints.VerifyToken, query: null, ct)
            .ConfigureAwait(false);
        return response.Result
            ?? throw new CloudflareApiException(new CloudflareApiError.Decoding("nil result for token verification"));
    }

    public async Task<IReadOnlyList<Account>> ListAccountsAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync<Account[]>(
                Endpoints.Accounts,
                query: [("per_page", "50")],
                ct)
            .ConfigureAwait(false);
        return response.Result ?? [];
    }

    public async Task<TokenValidationResult> ValidateAndFetchAccountsAsync(CancellationToken ct = default)
    {
        var verification = await VerifyAsync(ct).ConfigureAwait(false);
        var accounts = await ListAccountsAsync(ct).ConfigureAwait(false);
        return new TokenValidationResult(verification, accounts);
    }

    public static IReadOnlyList<Account> DeriveAccountsFromZones(IReadOnlyList<Zone> zones)
    {
        return zones
            .Where(z => z.AccountId is not null && z.AccountName is not null)
            .GroupBy(z => z.AccountId!)
            .Select(g => new Account(g.Key, g.First().AccountName!))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
