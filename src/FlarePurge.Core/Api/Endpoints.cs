using System;
using System.Collections.Generic;
using System.Text;

namespace FlarePurge.Core.Api;

public static class Endpoints
{
    public static readonly Uri Base = new("https://api.cloudflare.com/client/v4/");

    public const string VerifyToken = "/user/tokens/verify";
    public const string Accounts = "/accounts";
    public const string Zones = "/zones";

    public static string PurgeCache(string zoneId) => $"/zones/{zoneId}/purge_cache";

    public static Uri BuildUri(string path, IReadOnlyList<(string Key, string Value)>? query = null)
    {
        var builder = new UriBuilder(new Uri(Base, path.TrimStart('/')));
        if (query is { Count: > 0 })
        {
            var sb = new StringBuilder();
            foreach (var (k, v) in query)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
            }
            builder.Query = sb.ToString();
        }
        return builder.Uri;
    }
}
