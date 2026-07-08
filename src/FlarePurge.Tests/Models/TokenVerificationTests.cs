using System.Text.Json;
using FlarePurge.Core.Json;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Models;

public class TokenVerificationTests
{
    [Fact]
    public void Decode_ActiveToken_IsActiveTrue()
    {
        const string json = """
            { "id": "token-id", "status": "active" }
            """;

        var verification = JsonSerializer.Deserialize(json, CoreJsonContext.Default.TokenVerification);

        verification.Should().NotBeNull();
        verification!.Id.Should().Be("token-id");
        verification.Status.Should().Be("active");
        verification.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("pending")]
    public void Decode_NonActiveStatus_IsActiveFalse(string status)
    {
        var json = $$"""
            { "id": "t", "status": "{{status}}" }
            """;

        var verification = JsonSerializer.Deserialize(json, CoreJsonContext.Default.TokenVerification);

        verification!.IsActive.Should().BeFalse();
    }
}
