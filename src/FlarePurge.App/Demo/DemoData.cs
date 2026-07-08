using System;
using System.Collections.Generic;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Models;

namespace FlarePurge.App.Demo;

/// <summary>
/// Static seed data for -FPDemoMode 1. Two fake Cloudflare accounts and
/// eight zones spanning realistic plans and nameserver pairs. Used only
/// to generate Microsoft Store screenshots — never shipped as live data.
/// </summary>
internal static class DemoData
{
    public const string Account1Id = "demo-acc-colorvivo-0001";
    public const string Account2Id = "demo-acc-cloudnews-0002";

    public const string Account1Label = "Color Vivo Internet";
    public const string Account2Label = "Cloudnews Media";

    public static readonly IReadOnlyList<StoredAccount> StoredAccounts =
    [
        new StoredAccount(
            Id: "stored-" + Account1Id,
            CloudflareAccountId: Account1Id,
            Label: Account1Label,
            TokenKeychainAccount: "demo-token-1",
            AddedAt: DateTimeOffset.UtcNow.AddDays(-45)),
        new StoredAccount(
            Id: "stored-" + Account2Id,
            CloudflareAccountId: Account2Id,
            Label: Account2Label,
            TokenKeychainAccount: "demo-token-2",
            AddedAt: DateTimeOffset.UtcNow.AddDays(-12)),
    ];

    public static readonly IReadOnlyList<Account> Accounts =
    [
        new Account(Account1Id, Account1Label, "standard"),
        new Account(Account2Id, Account2Label, "standard"),
    ];

    public static readonly IReadOnlyList<Zone> Zones =
    [
        MakeZone("zone-colorvivo", "colorvivo.com", Account1Id, Account1Label, "Business", -320,
            "cal.ns.cloudflare.com", "jay.ns.cloudflare.com"),
        new Zone(
            Id: "zone-flarepurge",
            Name: "flarepurge.com",
            Status: "active",
            NameServers: ["rose.ns.cloudflare.com", "tom.ns.cloudflare.com"],
            AccountId: Account1Id,
            AccountName: Account1Label,
            PlanName: "Pro",
            CreatedOn: DateTimeOffset.UtcNow.AddDays(-92)),
        MakeZone("zone-password", "password.es", Account1Id, Account1Label, "Free", -210,
            "bob.ns.cloudflare.com", "lily.ns.cloudflare.com"),
        MakeZone("zone-spyonweb", "spyonweb.net", Account1Id, Account1Label, "Free", -175,
            "ken.ns.cloudflare.com", "sara.ns.cloudflare.com"),
        MakeZone("zone-cloudnews", "cloudnews.tech", Account2Id, Account2Label, "Pro", -540,
            "ian.ns.cloudflare.com", "rita.ns.cloudflare.com"),
        MakeZone("zone-noticias", "noticias.madrid", Account2Id, Account2Label, "Free", -58,
            "todd.ns.cloudflare.com", "uma.ns.cloudflare.com"),
        MakeZone("zone-revistacloud", "revistacloud.com", Account2Id, Account2Label, "Business", -610,
            "ada.ns.cloudflare.com", "leo.ns.cloudflare.com"),
        MakeZone("zone-actualitecloud", "actualitecloud.com", Account2Id, Account2Label, "Free", -14,
            "nick.ns.cloudflare.com", "olga.ns.cloudflare.com"),
    ];

    public static readonly IReadOnlyList<FavoriteZone> Favorites =
    [
        new FavoriteZone(
            Id: "zone-flarepurge",
            Name: "flarepurge.com",
            AccountId: Account1Id,
            AddedAt: DateTimeOffset.UtcNow.AddDays(-30)),
        new FavoriteZone(
            Id: "zone-colorvivo",
            Name: "colorvivo.com",
            AccountId: Account1Id,
            AddedAt: DateTimeOffset.UtcNow.AddDays(-25)),
    ];

    public static string DefaultActiveStoredAccountId => StoredAccounts[0].Id;

    private static Zone MakeZone(string id, string name, string accountId, string accountName,
        string plan, int daysAgo, string ns1, string ns2)
    {
        return new Zone(
            Id: id,
            Name: name,
            Status: "active",
            NameServers: [ns1, ns2],
            AccountId: accountId,
            AccountName: accountName,
            PlanName: plan,
            CreatedOn: DateTimeOffset.UtcNow.AddDays(daysAgo));
    }
}
