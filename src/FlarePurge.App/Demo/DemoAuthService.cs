using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;

namespace FlarePurge.App.Demo;

internal sealed class DemoAuthService : IAuthService
{
    public Task<TokenVerification> VerifyAsync(CancellationToken ct = default) =>
        Task.FromResult(new TokenVerification("demo-token-id", "active"));

    public Task<IReadOnlyList<Account>> ListAccountsAsync(CancellationToken ct = default) =>
        Task.FromResult(DemoData.Accounts);

    public Task<TokenValidationResult> ValidateAndFetchAccountsAsync(CancellationToken ct = default) =>
        Task.FromResult(new TokenValidationResult(
            new TokenVerification("demo-token-id", "active"),
            DemoData.Accounts));
}
