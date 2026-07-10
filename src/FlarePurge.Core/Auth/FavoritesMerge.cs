using System;
using System.Collections.Generic;
using System.Linq;

namespace FlarePurge.Core.Auth;

/// <summary>A zone currently visible in the list (the active account's zones)
/// together with its current favorite flag. Decouples the merge logic below from
/// the WinUI ViewModel so it can be unit-tested in the Core test project.</summary>
public readonly record struct VisibleZone(string Id, string Name, bool IsFavorite);

/// <summary>
/// Rebuilds the single GLOBAL favorites list after the user toggled favorites on
/// the ACTIVE account's visible zones.
///
/// Audit C1 (data loss): favorites live in one global list, but the visible zones
/// only ever belong to the active account. Deriving the whole list from the
/// visible zones would permanently wipe the favorites of every OTHER account (and
/// of any zone the server filtered out). Instead we preserve every existing
/// favorite whose zone is not currently visible, then recompute the visible ones
/// from their flag — reusing the existing entry (and its <c>AddedAt</c>) when the
/// zone stays a favorite so silent refreshes don't churn timestamps.
/// </summary>
public static class FavoritesMerge
{
    public static IReadOnlyList<FavoriteZone> Merge(
        IReadOnlyList<FavoriteZone> existing,
        IReadOnlyList<VisibleZone> visibleZones,
        string? ownerAccountId,
        DateTimeOffset now)
    {
        var existingById = existing.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var visibleIds = visibleZones.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);

        // Favorites of non-visible zones (other accounts) survive untouched.
        var preserved = existingById.Values.Where(f => !visibleIds.Contains(f.Id));

        // Visible zones marked favorite: keep the prior entry if present, else new.
        // A new favorite records the owning account (audit C4) so the tray can purge
        // it with that account's token even when another account is active.
        var current = visibleZones
            .Where(z => z.IsFavorite)
            .Select(z => existingById.TryGetValue(z.Id, out var prev)
                ? prev
                : new FavoriteZone(z.Id, z.Name, ownerAccountId, now));

        return preserved.Concat(current).ToArray();
    }
}
