using System;
using FlarePurge.Core.Api;
using FlarePurge.Core.Localization;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class CloudflareApiErrorTests : IDisposable
{
    public CloudflareApiErrorTests()
    {
        Localizer.ResetResolver();
    }

    public void Dispose() => Localizer.ResetResolver();

    [Theory]
    [InlineData(TokenProblem.Invalid, "error.unauthorized.invalid")]
    [InlineData(TokenProblem.Expired, "error.unauthorized.expired")]
    [InlineData(TokenProblem.Revoked, "error.unauthorized.revoked")]
    public void UserMessage_Unauthorized_SelectsProblemKey(TokenProblem problem, string expectedKey)
    {
        var error = new CloudflareApiError.Unauthorized(problem);

        error.UserMessage.Should().Be(expectedKey);
    }

    [Fact]
    public void UserMessage_ForbiddenWithScope_FormatsScope()
    {
        Localizer.SetResolver(key => key == "error.forbidden.missingScope" ? "missing {0}" : key);

        var error = new CloudflareApiError.Forbidden("Zone:Cache Purge");

        error.UserMessage.Should().Be("missing Zone:Cache Purge");
    }

    [Fact]
    public void UserMessage_ForbiddenGeneric_UsesGenericKey()
    {
        new CloudflareApiError.Forbidden(null).UserMessage
            .Should().Be("error.forbidden.generic");
    }

    [Fact]
    public void UserMessage_RateLimitedWithRetry_FormatsSeconds()
    {
        Localizer.SetResolver(key => key == "error.rateLimited.withRetry" ? "retry in {0}s" : key);

        var error = new CloudflareApiError.RateLimited(TimeSpan.FromSeconds(42));

        error.UserMessage.Should().Be("retry in 42s");
    }

    [Fact]
    public void UserMessage_RateLimitedGeneric_UsesGenericKey()
    {
        new CloudflareApiError.RateLimited(null).UserMessage
            .Should().Be("error.rateLimited.generic");
    }

    [Fact]
    public void UserMessage_NotFound_FormatsResource()
    {
        Localizer.SetResolver(key => key == "error.notFound" ? "not found: {0}" : key);

        new CloudflareApiError.NotFound("zones").UserMessage
            .Should().Be("not found: zones");
    }

    [Fact]
    public void UserMessage_NetworkUnavailable_UsesKey()
        => new CloudflareApiError.NetworkUnavailable().UserMessage
            .Should().Be("error.networkUnavailable");

    [Fact]
    public void UserMessage_Timeout_UsesKey()
        => new CloudflareApiError.Timeout().UserMessage.Should().Be("error.timeout");

    [Fact]
    public void UserMessage_CertificatePinningFailed_UsesKey()
        => new CloudflareApiError.CertificatePinningFailed().UserMessage
            .Should().Be("error.certPinning");

    [Fact]
    public void UserMessage_Decoding_FormatsMessage()
    {
        Localizer.SetResolver(key => key == "error.decoding" ? "decode failed: {0}" : key);

        new CloudflareApiError.Decoding("bad json").UserMessage
            .Should().Be("decode failed: bad json");
    }

    [Fact]
    public void UserMessage_ServerErrorWithMessage_FormatsMessage()
    {
        Localizer.SetResolver(key => key == "error.server.withMessage" ? "server: {0}" : key);

        new CloudflareApiError.ServerError(500, 1, "boom").UserMessage
            .Should().Be("server: boom");
    }

    [Fact]
    public void UserMessage_ServerErrorWithoutMessage_UsesGenericKey()
        => new CloudflareApiError.ServerError(500, null, null).UserMessage
            .Should().Be("error.server");

    [Fact]
    public void UserMessage_Cancelled_UsesKey()
        => new CloudflareApiError.Cancelled().UserMessage.Should().Be("error.cancelled");

    [Fact]
    public void UserMessage_Unknown_FormatsDetail()
    {
        Localizer.SetResolver(key => key == "error.unknown" ? "??? {0}" : key);

        new CloudflareApiError.Unknown("weirdness").UserMessage.Should().Be("??? weirdness");
    }

    [Fact]
    public void Recovery_Unauthorized_IsReauthenticate()
        => new CloudflareApiError.Unauthorized(TokenProblem.Invalid).Recovery
            .Should().Be(RecoveryAction.Reauthenticate);

    [Fact]
    public void Recovery_Forbidden_IsReauthenticate()
        => new CloudflareApiError.Forbidden(null).Recovery
            .Should().Be(RecoveryAction.Reauthenticate);

    [Fact]
    public void Recovery_RateLimitedWithRetry_IsRetryWithSameDelay()
    {
        var retry = TimeSpan.FromSeconds(12);

        new CloudflareApiError.RateLimited(retry).Recovery
            .Should().Be(new RecoveryAction.Retry(retry));
    }

    [Fact]
    public void Recovery_RateLimitedNoRetry_IsRetryWithNull()
        => new CloudflareApiError.RateLimited(null).Recovery
            .Should().Be(new RecoveryAction.Retry(null));

    [Fact]
    public void Recovery_NotFound_IsRefreshList()
        => new CloudflareApiError.NotFound("zones").Recovery
            .Should().Be(RecoveryAction.RefreshList);

    [Fact]
    public void Recovery_NetworkUnavailable_IsCheckConnection()
        => new CloudflareApiError.NetworkUnavailable().Recovery
            .Should().Be(RecoveryAction.CheckConnection);

    [Fact]
    public void Recovery_Timeout_IsRetryTwoSeconds()
        => new CloudflareApiError.Timeout().Recovery
            .Should().Be(new RecoveryAction.Retry(TimeSpan.FromSeconds(2)));

    [Fact]
    public void Recovery_CertificatePinningFailed_IsUpdateApp()
        => new CloudflareApiError.CertificatePinningFailed().Recovery
            .Should().Be(RecoveryAction.UpdateApp);

    [Theory]
    [InlineData(typeof(CloudflareApiError.Decoding))]
    [InlineData(typeof(CloudflareApiError.Cancelled))]
    [InlineData(typeof(CloudflareApiError.Unknown))]
    public void Recovery_OtherCases_AreNone(Type errorType)
    {
        CloudflareApiError error = errorType.Name switch
        {
            nameof(CloudflareApiError.Decoding) => new CloudflareApiError.Decoding("x"),
            nameof(CloudflareApiError.Cancelled) => new CloudflareApiError.Cancelled(),
            nameof(CloudflareApiError.Unknown) => new CloudflareApiError.Unknown("x"),
            _ => throw new InvalidOperationException()
        };

        error.Recovery.Should().Be(RecoveryAction.None);
    }

    [Fact]
    public void Recovery_ServerError_IsNone()
        => new CloudflareApiError.ServerError(502, null, null).Recovery
            .Should().Be(RecoveryAction.None);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsRetriable_RateLimited_IsTrue(bool withRetry)
    {
        var err = new CloudflareApiError.RateLimited(withRetry ? TimeSpan.FromSeconds(1) : null);
        err.IsRetriable.Should().BeTrue();
    }

    [Fact]
    public void IsRetriable_Timeout_IsTrue()
        => new CloudflareApiError.Timeout().IsRetriable.Should().BeTrue();

    [Fact]
    public void IsRetriable_NetworkUnavailable_IsTrue()
        => new CloudflareApiError.NetworkUnavailable().IsRetriable.Should().BeTrue();

    [Theory]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(599, true)]
    [InlineData(499, false)]
    [InlineData(600, false)]
    [InlineData(400, false)]
    public void IsRetriable_ServerError_TrueOnlyFor5xx(int status, bool expected)
    {
        new CloudflareApiError.ServerError(status, null, null).IsRetriable.Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(CloudflareApiError.Unauthorized))]
    [InlineData(typeof(CloudflareApiError.Forbidden))]
    [InlineData(typeof(CloudflareApiError.NotFound))]
    [InlineData(typeof(CloudflareApiError.CertificatePinningFailed))]
    [InlineData(typeof(CloudflareApiError.Decoding))]
    [InlineData(typeof(CloudflareApiError.Cancelled))]
    [InlineData(typeof(CloudflareApiError.Unknown))]
    public void IsRetriable_NonTransientCases_AreFalse(Type errorType)
    {
        CloudflareApiError error = errorType.Name switch
        {
            nameof(CloudflareApiError.Unauthorized) => new CloudflareApiError.Unauthorized(TokenProblem.Invalid),
            nameof(CloudflareApiError.Forbidden) => new CloudflareApiError.Forbidden(null),
            nameof(CloudflareApiError.NotFound) => new CloudflareApiError.NotFound("x"),
            nameof(CloudflareApiError.CertificatePinningFailed) => new CloudflareApiError.CertificatePinningFailed(),
            nameof(CloudflareApiError.Decoding) => new CloudflareApiError.Decoding("x"),
            nameof(CloudflareApiError.Cancelled) => new CloudflareApiError.Cancelled(),
            nameof(CloudflareApiError.Unknown) => new CloudflareApiError.Unknown("x"),
            _ => throw new InvalidOperationException()
        };

        error.IsRetriable.Should().BeFalse();
    }

    [Fact]
    public void Exception_ExposesUnderlyingError()
    {
        var err = new CloudflareApiError.NotFound("zones");

        var ex = new CloudflareApiException(err);

        ex.Error.Should().BeSameAs(err);
        ex.Message.Should().Be(err.UserMessage);
    }
}
