using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Api;
using FlarePurge.Core.Models;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class ApiClientTests
{
    private static readonly RateLimiterConfig FastConfig = new(
        MaxRetries: 2,
        BaseDelay: TimeSpan.FromMilliseconds(1),
        MaxDelay: TimeSpan.FromMilliseconds(5),
        JitterMin: 0.0,
        JitterMax: 0.0);

    private static ApiClient Build(FakeHttpMessageHandler handler, string? token = "test-token", RateLimiterConfig? config = null)
    {
        var http = new HttpClient(handler);
        return new ApiClient(http, new RateLimiter(config ?? FastConfig, new Random(1)), _ => ValueTask.FromResult(token));
    }

    [Fact]
    public async Task Get_Success_DeserializesEnvelope()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(
            HttpStatusCode.OK,
            """{"success":true,"errors":[],"messages":[],"result":{"id":"t","status":"active"}}""");
        var client = Build(handler);

        var response = await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        response.Success.Should().BeTrue();
        response.Result!.Id.Should().Be("t");
        handler.Received.Should().ContainSingle()
            .Which.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "test-token"));
    }

    [Fact]
    public async Task Get_NoToken_OmitsAuthorizationHeader()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(
            HttpStatusCode.OK,
            """{"success":true,"errors":[],"messages":[],"result":{"id":"t","status":"active"}}""");
        var client = Build(handler, token: null);

        await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        handler.Received[0].Headers.Authorization.Should().BeNull();
    }

    [Theory]
    [InlineData(10001, TokenProblem.Invalid)]
    [InlineData(6003, TokenProblem.Invalid)]
    [InlineData(10000, TokenProblem.Expired)]
    [InlineData(99999, TokenProblem.Invalid)]
    public async Task Get_401_MapsCodeToTokenProblem(int cfCode, TokenProblem expected)
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(
            HttpStatusCode.Unauthorized,
            $$"""{"success":false,"errors":[{"code":{{cfCode}},"message":"auth"}],"messages":[],"result":null}""");
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unauthorized>()
            .Which.Problem.Should().Be(expected);
    }

    [Fact]
    public async Task Get_401_NoEnvelope_FallsBackToInvalid()
    {
        var handler = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.Unauthorized);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unauthorized>()
            .Which.Problem.Should().Be(TokenProblem.Invalid);
    }

    [Fact]
    public async Task Get_403_ReturnsForbiddenWithNullScope()
    {
        var handler = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.Forbidden);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Forbidden>()
            .Which.MissingScope.Should().BeNull();
    }

    [Fact]
    public async Task Get_404_IncludesPath()
    {
        var handler = new FakeHttpMessageHandler().EnqueueStatus(HttpStatusCode.NotFound);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>("/zones/missing");

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.NotFound>()
            .Which.Resource.Should().EndWith("/zones/missing");
    }

    [Fact]
    public async Task Get_429_WithRetryAfter_RetriesAndSucceeds()
    {
        var handler = new FakeHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.TooManyRequests, r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1)))
            .EnqueueJson(HttpStatusCode.OK, """{"success":true,"errors":[],"messages":[],"result":{"id":"t","status":"active"}}""");
        var client = Build(handler);

        var response = await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        response.Success.Should().BeTrue();
        handler.Received.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_429_ExhaustedRetries_ThrowsRateLimited()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 5; i++)
            handler.EnqueueStatus(HttpStatusCode.TooManyRequests);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.RateLimited>();
        handler.Received.Should().HaveCount(FastConfig.MaxRetries + 1);
    }

    [Fact]
    public async Task Get_500_WithCfCode_ReturnsServerErrorAndRetries()
    {
        var handler = new FakeHttpMessageHandler()
            .EnqueueJson(HttpStatusCode.InternalServerError, """{"success":false,"errors":[{"code":3,"message":"boom"}],"messages":[],"result":null}""")
            .EnqueueJson(HttpStatusCode.OK, """{"success":true,"errors":[],"messages":[],"result":{"id":"t","status":"active"}}""");
        var client = Build(handler);

        var response = await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        response.Success.Should().BeTrue();
        handler.Received.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_500_ExhaustedRetries_PropagatesServerErrorWithBody()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 5; i++)
            handler.EnqueueJson(HttpStatusCode.InternalServerError, """{"success":false,"errors":[{"code":99,"message":"db down"}],"messages":[],"result":null}""");
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        var server = ex.Error.Should().BeOfType<CloudflareApiError.ServerError>().Subject;
        server.StatusCode.Should().Be(500);
        server.CfCode.Should().Be(99);
        server.Message.Should().Be("db down");
    }

    [Fact]
    public async Task Get_418_UnknownStatus_ReturnsUnknown()
    {
        var handler = new FakeHttpMessageHandler().EnqueueStatus((HttpStatusCode)418);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unknown>();
    }

    [Fact]
    public async Task Get_NetworkError_MapsToNetworkUnavailable()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 5; i++)
            handler.EnqueueException(new HttpRequestException("dns failed", new SocketException((int)SocketError.HostNotFound)));
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.NetworkUnavailable>();
        handler.Received.Should().HaveCount(FastConfig.MaxRetries + 1);
    }

    [Fact]
    public async Task Get_CertificateFailure_MapsToCertificatePinningFailed()
    {
        var handler = new FakeHttpMessageHandler().EnqueueException(
            new HttpRequestException("The SSL connection could not be established", new AuthenticationException("pin mismatch")));
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.CertificatePinningFailed>();
    }

    [Fact]
    public async Task Get_SecureConnectionError_NoKeywordsInMessage_MapsToCertificatePinningFailed()
    {
        // N5: rely on the typed HttpRequestError, not on sniffing "SSL/TLS/certificate"
        // out of a (possibly localized) message. This message has none of those words.
        var handler = new FakeHttpMessageHandler().EnqueueException(
            new HttpRequestException(HttpRequestError.SecureConnectionError, "conexión no establecida"));
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.CertificatePinningFailed>();
    }

    [Fact]
    public async Task Get_UnrelatedHttpException_MapsToUnknown()
    {
        var handler = new FakeHttpMessageHandler().EnqueueException(new HttpRequestException("something weird"));
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Unknown>();
    }

    [Fact]
    public async Task Get_CancelledToken_MapsToCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler().EnqueueCanceled(cts.Token);
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken, ct: cts.Token);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Cancelled>();
    }

    [Fact]
    public async Task Get_Timeout_MapsToTimeout()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 5; i++)
            handler.EnqueueException(new TaskCanceledException("timed out"));
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Timeout>();
    }

    [Fact]
    public async Task Get_InvalidJsonBody_MapsToDecoding()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "not-json");
        var client = Build(handler);

        var act = async () => await client.GetAsync<TokenVerification>(Endpoints.VerifyToken);

        var ex = (await act.Should().ThrowAsync<CloudflareApiException>()).Which;
        ex.Error.Should().BeOfType<CloudflareApiError.Decoding>();
    }

    [Fact]
    public async Task Post_SendsJsonBodyWithContentType()
    {
        var handler = new FakeHttpMessageHandler().EnqueueJson(
            HttpStatusCode.OK,
            """{"success":true,"errors":[],"messages":[],"result":{"id":"purge-1"}}""");
        var client = Build(handler);

        var response = await client.PostAsync<CachePurgeRequest, CachePurgeResult>(
            Endpoints.PurgeCache("z"),
            CachePurgeRequest.Everything);

        response.Result!.Id.Should().Be("purge-1");
        var req = handler.Received.Should().ContainSingle().Subject;
        req.Method.Should().Be(HttpMethod.Post);
        handler.ReceivedBodies.Should().ContainSingle()
            .Which.Should().Be("""{"purge_everything":true}""");
    }

    [Fact]
    public void Ctor_NullHttp_Throws()
    {
        var act = () => new ApiClient(null!, new RateLimiter(), _ => ValueTask.FromResult<string?>(null));

        act.Should().Throw<ArgumentNullException>();
    }
}
