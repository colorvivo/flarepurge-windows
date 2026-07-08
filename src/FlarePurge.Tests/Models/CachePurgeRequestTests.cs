using System.Text.Json;
using FlarePurge.Core.Json;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Models;

public class CachePurgeRequestTests
{
    [Fact]
    public void Everything_SerializesOnlyPurgeEverythingFlag()
    {
        var json = JsonSerializer.Serialize(CachePurgeRequest.Everything, CoreJsonContext.Default.CachePurgeRequest);

        json.Should().Be("""{"purge_everything":true}""");
    }

    [Fact]
    public void FromFiles_SerializesOnlyFilesArray()
    {
        var request = CachePurgeRequest.FromFiles(new[] { "https://example.com/a", "https://example.com/b" });

        var json = JsonSerializer.Serialize(request, CoreJsonContext.Default.CachePurgeRequest);

        json.Should().Be("""{"files":["https://example.com/a","https://example.com/b"]}""");
    }

    [Fact]
    public void FromHosts_SerializesOnlyHostsArray()
    {
        var request = CachePurgeRequest.FromHosts(new[] { "example.com", "cdn.example.com" });

        var json = JsonSerializer.Serialize(request, CoreJsonContext.Default.CachePurgeRequest);

        json.Should().Be("""{"hosts":["example.com","cdn.example.com"]}""");
    }

    [Fact]
    public void DefaultRequest_OmitsAllNullFields()
    {
        var request = new CachePurgeRequest();

        var json = JsonSerializer.Serialize(request, CoreJsonContext.Default.CachePurgeRequest);

        json.Should().Be("{}");
    }

    [Fact]
    public void CachePurgeResult_DeserializesId()
    {
        const string json = """{ "id": "purge-12345" }""";

        var result = JsonSerializer.Deserialize(json, CoreJsonContext.Default.CachePurgeResult);

        result!.Id.Should().Be("purge-12345");
    }
}
