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

    public static string PurgeCache(string zoneId)
    {
        // The zone id is interpolated straight into the request path. It reaches
        // here from disk cache and server payloads, so reject anything outside the
        // URL-path-safe id charset — otherwise a crafted value (`../`, `/`, an
        // encoded separator, whitespace) could inject extra path segments (audit
        // N2). Cloudflare ids are 32 hex chars; this also accepts the shorter
        // placeholders used in tests.
        if (!IsPathSafeId(zoneId))
            throw new ArgumentException("Zone id contains characters that are not URL-path safe.", nameof(zoneId));
        return $"/zones/{zoneId}/purge_cache";
    }

    /// <summary>True if <paramref name="id"/> is a non-empty run of ASCII
    /// letters, digits, hyphen or underscore — safe to interpolate into a path.</summary>
    public static bool IsPathSafeId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (var c in id)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
                return false;
        }
        return true;
    }

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
