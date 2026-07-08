using System.Text.Json;
using FlarePurge.Core.Json;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Models;

public class AccountTests
{
    [Fact]
    public void Decode_MinimalAccount_HasIdAndName()
    {
        const string json = """
            { "id": "abc123", "name": "My Cloudflare Account" }
            """;

        var account = JsonSerializer.Deserialize(json, CoreJsonContext.Default.Account);

        account.Should().NotBeNull();
        account!.Id.Should().Be("abc123");
        account.Name.Should().Be("My Cloudflare Account");
        account.Type.Should().BeNull();
    }

    [Fact]
    public void Decode_FullAccount_HasType()
    {
        const string json = """
            { "id": "abc123", "name": "My Account", "type": "standard" }
            """;

        var account = JsonSerializer.Deserialize(json, CoreJsonContext.Default.Account);

        account!.Type.Should().Be("standard");
    }
}
