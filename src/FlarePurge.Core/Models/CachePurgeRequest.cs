using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Models;

public sealed record CachePurgeRequest(
    [property: JsonPropertyName("purge_everything")] bool? PurgeEverything = null,
    [property: JsonPropertyName("files")] IReadOnlyList<string>? Files = null,
    [property: JsonPropertyName("hosts")] IReadOnlyList<string>? Hosts = null,
    [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags = null,
    [property: JsonPropertyName("prefixes")] IReadOnlyList<string>? Prefixes = null)
{
    public static readonly CachePurgeRequest Everything = new(PurgeEverything: true);

    public static CachePurgeRequest FromFiles(IReadOnlyList<string> files) => new(Files: files);

    public static CachePurgeRequest FromHosts(IReadOnlyList<string> hosts) => new(Hosts: hosts);
}

public sealed record CachePurgeResult(
    [property: JsonPropertyName("id")] string Id);
