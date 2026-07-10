using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Models;

public sealed class ZoneJsonConverter : JsonConverter<Zone>
{
    public override Zone Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // GetProperty(...) throws KeyNotFoundException on a missing key and
        // GetString() throws InvalidOperationException on a non-string value —
        // neither is a JsonException, so they would escape ApiClient's decoding
        // catch and crash. Require the field as a JsonException instead so a
        // malformed 200 response maps cleanly to CloudflareApiError.Decoding.
        static string RequireString(JsonElement obj, string prop)
        {
            if (obj.TryGetProperty(prop, out var el)
                && el.ValueKind == JsonValueKind.String
                && el.GetString() is { } value)
            {
                return value;
            }
            throw new JsonException($"Zone.{prop} missing or not a string");
        }

        var id = RequireString(root, "id");
        var name = RequireString(root, "name");
        var status = RequireString(root, "status");

        IReadOnlyList<string>? nameServers = null;
        if (root.TryGetProperty("name_servers", out var ns) && ns.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>(ns.GetArrayLength());
            foreach (var el in ns.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && el.GetString() is { } s)
                    list.Add(s);
            }
            nameServers = list;
        }

        string? accountId = null;
        string? accountName = null;
        if (root.TryGetProperty("account", out var acc) && acc.ValueKind == JsonValueKind.Object)
        {
            if (acc.TryGetProperty("id", out var aid) && aid.ValueKind == JsonValueKind.String)
                accountId = aid.GetString();
            if (acc.TryGetProperty("name", out var aname) && aname.ValueKind == JsonValueKind.String)
                accountName = aname.GetString();
        }

        string? planName = null;
        if (root.TryGetProperty("plan", out var plan) && plan.ValueKind == JsonValueKind.Object)
        {
            if (plan.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String)
                planName = pn.GetString();
        }

        DateTimeOffset? createdOn = null;
        if (root.TryGetProperty("created_on", out var co) && co.ValueKind == JsonValueKind.String)
        {
            var raw = co.GetString();
            if (!string.IsNullOrWhiteSpace(raw) && DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                createdOn = parsed;
            }
        }

        return new Zone(id, name, status, nameServers, accountId, accountName, planName, createdOn);
    }

    public override void Write(Utf8JsonWriter writer, Zone value, JsonSerializerOptions options)
        => throw new NotSupportedException("Zone is read-only from the Cloudflare API; serialization is not supported.");
}
