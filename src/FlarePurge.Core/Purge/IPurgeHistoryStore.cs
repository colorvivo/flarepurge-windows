using System;
using System.Collections.Generic;

namespace FlarePurge.Core.Purge;

public interface IPurgeHistoryStore
{
    event EventHandler? Changed;
    IReadOnlyList<PurgeHistoryEntry> GetAll();
    void Record(PurgeHistoryEntry entry);
    void Clear();
    int Count { get; }
}
