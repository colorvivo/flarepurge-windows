using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Tests.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Auth;

public class TokenVerifierTests
{
    [Fact]
    public async Task ValidateAndFetchAsync_Success_ReturnsVerificationAndAccounts()
    {
        var handler = new FakeHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.OK,
                """{"success":true,"errors":[],"messages":[],"result":{"id":"tok","status":"active"}}""")
            .EnqueueJson(HttpStatusCode.OK,
                """{"success":true,"errors":[],"messages":[],"result":[{"id":"acc-1","name":"Alpha"}]}""");
        var http = new HttpClient(handler);

        var result = await TokenVerifier.ValidateAndFetchAsync(http, "the-token");

        result.Verification.IsActive.Should().BeTrue();
        result.Accounts.Should().ContainSingle().Which.Id.Should().Be("acc-1");

        handler.Received.Should().HaveCount(2);
        handler.Received[0].Headers.Authorization
            .Should().Be(new AuthenticationHeaderValue("Bearer", "the-token"));
    }

    [Fact]
    public async Task ValidateAndFetchAsync_InvalidToken_ThrowsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(
            HttpStatusCode.Unauthorized,
            """{"success":false,"errors":[{"code":10001,"message":"bad"}],"messages":[],"result":null}""");
        var http = new HttpClient(handler);

        var act = async () => await TokenVerifier.ValidateAndFetchAsync(http, "bad-token");

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unauthorized>()
            .Which.Problem.Should().Be(TokenProblem.Invalid);
    }
}
