using System;
using System.Collections.Generic;

namespace FlarePurge.Core.Purge;

public sealed class InMemoryPurgeHistoryStore : IPurgeHistoryStore
{
    private readonly List<PurgeHistoryEntry> _entries = new();
    private readonly object _gate = new();

    public event EventHandler? Changed;

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public IReadOnlyList<PurgeHistoryEntry> GetAll()
    {
        lock (_gate) return _entries.ToArray();
    }

    public void Record(PurgeHistoryEntry entry)
    {
        lock (_gate) _entries.Add(entry);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        bool changed;
        lock (_gate)
        {
            changed = _entries.Count > 0;
            _entries.Clear();
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }
}
