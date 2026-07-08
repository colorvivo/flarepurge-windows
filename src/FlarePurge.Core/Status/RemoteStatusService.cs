using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Json;

namespace FlarePurge.Core.Status;

public sealed class RemoteStatusService : IRemoteStatusService, IDisposable
{
    public static readonly Uri StatusEndpoint = new("https://flarepurge.com/status.json");

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    // Parameterless ctor: owns a fresh HttpClient with short timeouts so app
    // startup never hangs on an unreachable status endpoint. Tests can pass
    // their own client (e.g. with a mocked handler) via the other overload.
    public RemoteStatusService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        _ownsClient = true;
    }

    public RemoteStatusService(HttpClient http)
    {
        _http = http;
        _ownsClient = false;
    }

    public async Task<RemoteStatus> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(StatusEndpoint, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return RemoteStatus.Enabled;

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var status = await JsonSerializer.DeserializeAsync(
                stream,
                CoreJsonContext.Default.RemoteStatus,
                ct).ConfigureAwait(false);
            return status ?? RemoteStatus.Enabled;
        }
        catch
        {
            // Fail-open: if the endpoint is unreachable, the cert fails, the JSON is
            // malformed, or anything else goes sideways, assume the app is enabled.
            // Kill switch is a safety valve, not a security gate.
            return RemoteStatus.Enabled;
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
