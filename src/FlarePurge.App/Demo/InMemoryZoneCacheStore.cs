using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;

namespace FlarePurge.App.Demo;

internal sealed class InMemoryZoneCacheStore : IZoneCacheStore
{
    private readonly ConcurrentDictionary<string, ZoneCacheEntry> _entries = new();

    public ZoneCacheEntry? Get(string localAccountId)
        => _entries.TryGetValue(localAccountId, out var entry) ? entry : null;

    public void Save(string localAccountId, IReadOnlyList<Zone> zones)
    {
        if (string.IsNullOrEmpty(localAccountId)) return;
        var cached = zones.Select(CachedZone.From).ToArray();
        _entries[localAccountId] = new ZoneCacheEntry(localAccountId, System.DateTimeOffset.UtcNow, cached);
    }

    public void Delete(string localAccountId) => _entries.TryRemove(localAccountId, out _);

    public void Clear() => _entries.Clear();
}
