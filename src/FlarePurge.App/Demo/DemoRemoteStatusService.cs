using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Status;

namespace FlarePurge.App.Demo;

internal sealed class DemoRemoteStatusService : IRemoteStatusService
{
    public Task<RemoteStatus> FetchAsync(CancellationToken ct = default) =>
        Task.FromResult(RemoteStatus.Enabled);
}
