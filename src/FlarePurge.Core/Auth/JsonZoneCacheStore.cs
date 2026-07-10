using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlarePurge.Core.Json;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Auth;

public sealed class JsonZoneCacheStore : IZoneCacheStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonZoneCacheStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public ZoneCacheEntry? Get(string localAccountId)
    {
        lock (_lock)
        {
            return Load().Entries.FirstOrDefault(e => string.Equals(e.AccountId, localAccountId, StringComparison.Ordinal));
        }
    }

    public void Save(string localAccountId, IReadOnlyList<Zone> zones)
    {
        if (string.IsNullOrEmpty(localAccountId)) return;
        lock (_lock)
        {
            var current = Load();
            var remaining = current.Entries
                .Where(e => !string.Equals(e.AccountId, localAccountId, StringComparison.Ordinal))
                .ToList();
            var cached = zones.Select(CachedZone.From).ToArray();
            remaining.Add(new ZoneCacheEntry(localAccountId, DateTimeOffset.UtcNow, cached));
            Write(new ZoneCacheData(remaining));
        }
    }

    public void Delete(string localAccountId)
    {
        lock (_lock)
        {
            var current = Load();
            var remaining = current.Entries
                .Where(e => !string.Equals(e.AccountId, localAccountId, StringComparison.Ordinal))
                .ToArray();
            if (remaining.Length == current.Entries.Count) return;
            Write(new ZoneCacheData(remaining));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    private ZoneCacheData Load()
    {
        if (!File.Exists(_path)) return new ZoneCacheData(Array.Empty<ZoneCacheEntry>());
        try
        {
            using var stream = File.OpenRead(_path);
            var data = JsonSerializer.Deserialize(stream, CoreJsonContext.Default.ZoneCacheData);
            if (data is null) return new ZoneCacheData(Array.Empty<ZoneCacheEntry>());
            // A valid file with "entries": null would NRE on the next Get().
            return data.Entries is null ? data with { Entries = Array.Empty<ZoneCacheEntry>() } : data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable — degrade to empty rather than crash.
            return new ZoneCacheData(Array.Empty<ZoneCacheEntry>());
        }
    }

    private void Write(ZoneCacheData data)
        => AtomicFile.Write(_path, stream =>
            JsonSerializer.Serialize(stream, data, CoreJsonContext.Default.ZoneCacheData));

    public static string DefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "FlarePurge", "zones.v1.json");
    }
}
