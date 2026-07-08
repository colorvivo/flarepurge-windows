using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;

namespace FlarePurge.Core.Auth;

public static class TokenVerifier
{
    // Verifies a token that is not yet stored anywhere. The production
    // ApiClient derives its Authorization header from the active-account
    // token provider (keychain lookup), but the token wizard has the token
    // in hand and must validate it before we persist anything. We spin up a
    // one-shot ApiClient backed by the caller's HttpClient (same pinning
    // handler, same base address) and a closure that always returns the
    // candidate token, then let AuthService.ValidateAndFetchAccountsAsync
    // do its usual Verify → ListAccounts dance.
    public static Task<TokenValidationResult> ValidateAndFetchAsync(
        HttpClient http,
        string token,
        CancellationToken ct = default)
    {
        var client = new ApiClient(http, new RateLimiter(), _ => ValueTask.FromResult<string?>(token));
        return new AuthService(client).ValidateAndFetchAccountsAsync(ct);
    }
}
