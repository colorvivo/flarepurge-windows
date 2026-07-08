using System.Threading;
using System.Threading.Tasks;

namespace FlarePurge.Core.Status;

public interface IRemoteStatusService
{
    Task<RemoteStatus> FetchAsync(CancellationToken ct = default);
}
