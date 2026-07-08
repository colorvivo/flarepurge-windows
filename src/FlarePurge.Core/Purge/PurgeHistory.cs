using System;

namespace FlarePurge.Core.Purge;

public enum PurgeKind
{
    Everything,
    Urls,
    Hosts,
    BulkFavorites,
    BulkAccount,
}

public sealed record PurgeHistoryEntry(
    DateTimeOffset Timestamp,
    PurgeKind Kind,
    string ZoneOrAccount,
    int Count,
    bool Success,
    string? PurgeId,
    string? ErrorMessage);
