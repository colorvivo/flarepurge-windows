using System;
using System.Text.Json;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Models;

public class ZoneTests
{
    [Fact]
    public void Decode_FullZone_MapsAllFields()
    {
        const string json = """
            {
              "id": "zone-id-1",
              "name": "example.com",
              "status": "active",
              "name_servers": ["ns1.cloudflare.com", "ns2.cloudflare.com"],
              "account": { "id": "acc-id", "name": "My Account" },
              "plan": { "name": "Free" },
              "created_on": "2023-01-15T10:30:00.1234567Z"
            }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone.Should().NotBeNull();
        zone!.Id.Should().Be("zone-id-1");
        zone.Name.Should().Be("example.com");
        zone.Status.Should().Be("active");
        zone.NameServers.Should().BeEquivalentTo(new[] { "ns1.cloudflare.com", "ns2.cloudflare.com" });
        zone.AccountId.Should().Be("acc-id");
        zone.AccountName.Should().Be("My Account");
        zone.PlanName.Should().Be("Free");
        zone.CreatedOn.Should().Be(new DateTimeOffset(2023, 1, 15, 10, 30, 0, 123, TimeSpan.Zero).AddTicks(4567));
        zone.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Decode_CreatedOnWithoutFractionalSeconds_Parses()
    {
        const string json = """
            {
              "id": "z",
              "name": "example.com",
              "status": "active",
              "created_on": "2023-01-15T10:30:00Z"
            }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.CreatedOn.Should().Be(new DateTimeOffset(2023, 1, 15, 10, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Decode_MissingAccount_AccountFieldsNull()
    {
        const string json = """
            { "id": "z", "name": "a.com", "status": "active" }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.AccountId.Should().BeNull();
        zone.AccountName.Should().BeNull();
    }

    [Fact]
    public void Decode_MissingPlan_PlanNameNull()
    {
        const string json = """
            { "id": "z", "name": "a.com", "status": "active" }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.PlanName.Should().BeNull();
    }

    [Fact]
    public void Decode_MissingNameServers_NameServersNull()
    {
        const string json = """
            { "id": "z", "name": "a.com", "status": "active" }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.NameServers.Should().BeNull();
    }

    [Fact]
    public void Decode_EmptyNameServersArray_ReturnsEmptyList()
    {
        const string json = """
            { "id": "z", "name": "a.com", "status": "active", "name_servers": [] }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.NameServers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Decode_MissingCreatedOn_CreatedOnNull()
    {
        const string json = """
            { "id": "z", "name": "a.com", "status": "active" }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.CreatedOn.Should().BeNull();
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("moved")]
    [InlineData("deactivated")]
    public void IsActive_OnlyTrueWhenStatusIsActive(string status)
    {
        var json = $$"""
            { "id": "z", "name": "a.com", "status": "{{status}}" }
            """;

        var zone = JsonSerializer.Deserialize<Zone>(json);

        zone!.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Serialize_ThrowsNotSupported()
    {
        var zone = new Zone("z", "a.com", "active", null, null, null, null, null);

        var act = () => JsonSerializer.Serialize(zone);

        act.Should().Throw<NotSupportedException>();
    }

    // --- H1 (audit): a 200 response carrying a malformed Zone must surface as a
    // JsonException so ApiClient's `catch (JsonException)` maps it to
    // CloudflareApiError.Decoding. The pre-fix converter threw
    // KeyNotFoundException / InvalidOperationException, which escaped that catch
    // and crashed the process. ---

    [Theory]
    [InlineData("""{ "name": "a.com", "status": "active" }""")]  // id missing
    [InlineData("""{ "id": "z", "status": "active" }""")]        // name missing
    [InlineData("""{ "id": "z", "name": "a.com" }""")]           // status missing
    public void Decode_MissingRequiredField_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<Zone>(json);

        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("""{ "id": 123, "name": "a.com", "status": "active" }""")]   // id not a string
    [InlineData("""{ "id": "z", "name": null, "status": "active" }""")]      // name is null
    [InlineData("""{ "id": "z", "name": "a.com", "status": ["active"] }""")] // status is an array
    public void Decode_RequiredFieldWrongType_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<Zone>(json);

        act.Should().Throw<JsonException>();
    }
}
