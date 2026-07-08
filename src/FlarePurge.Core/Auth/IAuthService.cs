using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Auth;

public interface IAuthService
{
    Task<TokenVerification> VerifyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Account>> ListAccountsAsync(CancellationToken ct = default);
    Task<TokenValidationResult> ValidateAndFetchAccountsAsync(CancellationToken ct = default);
}

public sealed record TokenValidationResult(
    TokenVerification Verification,
    IReadOnlyList<Account> Accounts);
