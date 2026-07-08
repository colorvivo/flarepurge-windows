using System.Collections.Generic;
using System.Linq;
using FlarePurge.Core.Api;

namespace FlarePurge.Core.Models;

public sealed record CachePurgeBatchResult(IReadOnlyList<CachePurgeBatchResult.ChunkOutcome> Chunks)
{
    public sealed record ChunkOutcome(int ChunkIndex, int UrlCount, string? PurgeId, CloudflareApiError? Failure)
    {
        public bool IsSuccess => Failure is null;
    }

    public int SuccessCount => Chunks.Count(c => c.IsSuccess);
    public int FailureCount => Chunks.Count - SuccessCount;
    public bool IsFullSuccess => FailureCount == 0;
    public string? FirstPurgeId => Chunks.FirstOrDefault(c => c.PurgeId is not null)?.PurgeId;
    public CloudflareApiError? FirstFailure => Chunks.FirstOrDefault(c => c.Failure is not null)?.Failure;
}
