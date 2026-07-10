using System;
using System.Linq;
using FlarePurge.Core.Auth;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

// Audit C1 (data loss): the global favorites list must survive editing favorites
// on one account while the other accounts' zones are not visible. FavoritesMerge
// is the pure core of ZoneListViewModel.PersistFavorites, extracted so it can be
// tested without the WinUI ViewModel (which needs a packaged ResourceLoader).
public class FavoritesMergeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
    private const string OwnerId = "acc-active";

    [Fact]
    public void Merge_TogglingInOneAccount_PreservesOtherAccountsFavorites()
    {
        // Account A's favorite is persisted but NOT in the visible set (account B active).
        var existing = new[] { new FavoriteZone("a1", "alpha.com", "acc-a", T0) };
        var visible = new[]
        {
            new VisibleZone("b1", "beta.com", IsFavorite: true),   // just favorited in B
            new VisibleZone("b2", "gamma.com", IsFavorite: false),
        };

        var result = FavoritesMerge.Merge(existing, visible, OwnerId, Now);

        result.Select(f => f.Id).Should().BeEquivalentTo(new[] { "a1", "b1" });
    }

    [Fact]
    public void Merge_EmptyVisibleZones_PreservesAllExisting()
    {
        // The pre-fix bug: an active account whose zones failed to load (visible
        // empty) would derive an empty global list and wipe every favorite.
        var existing = new[]
        {
            new FavoriteZone("a1", "alpha.com", "acc-a", T0),
            new FavoriteZone("b1", "beta.com", "acc-b", T0),
        };

        var result = FavoritesMerge.Merge(existing, Array.Empty<VisibleZone>(), OwnerId, Now);

        result.Should().BeEquivalentTo(existing);
    }

    [Fact]
    public void Merge_UnfavoritingVisibleZone_RemovesItButKeepsOthers()
    {
        var existing = new[]
        {
            new FavoriteZone("a1", "alpha.com", "acc-a", T0),   // other account, preserved
            new FavoriteZone("v1", "visible.com", OwnerId, T0), // visible, being unfavorited
        };
        var visible = new[] { new VisibleZone("v1", "visible.com", IsFavorite: false) };

        var result = FavoritesMerge.Merge(existing, visible, OwnerId, Now);

        result.Select(f => f.Id).Should().BeEquivalentTo(new[] { "a1" });
    }

    [Fact]
    public void Merge_StillFavoriteVisibleZone_ReusesExistingEntryWithoutChurn()
    {
        // A silent refresh re-runs PersistFavorites; the kept favorite must retain
        // its original AddedAt/AccountId/Name, not be rebuilt with `now`.
        var existing = new[] { new FavoriteZone("v1", "old-name.com", "acc-x", T0) };
        var visible = new[] { new VisibleZone("v1", "new-name.com", IsFavorite: true) };

        var result = FavoritesMerge.Merge(existing, visible, OwnerId, Now);

        result.Should().ContainSingle();
        var kept = result[0];
        kept.Id.Should().Be("v1");
        kept.Name.Should().Be("old-name.com");
        kept.AccountId.Should().Be("acc-x");
        kept.AddedAt.Should().Be(T0);
    }

    [Fact]
    public void Merge_NewlyFavoritedVisibleZone_RecordsOwnerAccount()
    {
        // C4: a new favorite stores the owning account so the tray can purge it
        // with the right token later.
        var visible = new[] { new VisibleZone("n1", "new.com", IsFavorite: true) };

        var result = FavoritesMerge.Merge(Array.Empty<FavoriteZone>(), visible, OwnerId, Now);

        result.Should().ContainSingle();
        result[0].Should().Be(new FavoriteZone("n1", "new.com", OwnerId, Now));
    }
}
