using System.Collections.Generic;
using FlarePurge.Core.Models;

namespace FlarePurge.Core.Auth;

public interface IZoneCacheStore
{
    ZoneCacheEntry? Get(string localAccountId);
    void Save(string localAccountId, IReadOnlyList<Zone> zones);
    void Delete(string localAccountId);
    void Clear();
}

