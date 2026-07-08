using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlarePurge.Core.Api;

public interface IApiClient
{
    Task<ApiResponse<T>> GetAsync<T>(
        string path,
        IReadOnlyList<(string Key, string Value)>? query = null,
        CancellationToken ct = default);

    Task<ApiResponse<T>> PostAsync<TBody, T>(
        string path,
        TBody body,
        CancellationToken ct = default);
}
