using System;
using FlarePurge.Core.Api;
using FluentAssertions;
using Xunit;

namespace FlarePurge.Tests.Api;

public class RateLimiterTests
{
    private static readonly RateLimiterConfig ZeroJitter = new(
        MaxRetries: 4,
        BaseDelay: TimeSpan.FromSeconds(1),
        MaxDelay: TimeSpan.FromSeconds(60),
        JitterMin: 0.0,
        JitterMax: 0.0);

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 2.0)]
    [InlineData(2, 4.0)]
    [InlineData(3, 8.0)]
    public void BackoffDelay_NoRetryAfter_ExponentialBase2(int attempt, double expectedSeconds)
    {
        var limiter = new RateLimiter(ZeroJitter, new Random(42));

        var delay = limiter.BackoffDelay(attempt, retryAfter: null);

        delay.TotalSeconds.Should().BeApproximately(expectedSeconds, 0.001);
    }

    [Fact]
    public void BackoffDelay_RetryAfterBelowMax_UsedAsIs()
    {
        var limiter = new RateLimiter(ZeroJitter);

        var delay = limiter.BackoffDelay(0, TimeSpan.FromSeconds(5));

        delay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BackoffDelay_RetryAfterExceedsMax_CappedAtMaxDelay()
    {
        var limiter = new RateLimiter(ZeroJitter);

        var delay = limiter.BackoffDelay(0, TimeSpan.FromSeconds(120));

        delay.Should().Be(ZeroJitter.MaxDelay);
    }

    [Fact]
    public void BackoffDelay_HighAttemptCount_CappedAtMaxDelay()
    {
        var limiter = new RateLimiter(ZeroJitter);

        // 2^20 s >> 60 s cap
        var delay = limiter.BackoffDelay(20, retryAfter: null);

        delay.Should().Be(ZeroJitter.MaxDelay);
    }

    [Fact]
    public void BackoffDelay_JitterStaysWithinConfiguredRange()
    {
        var config = new RateLimiterConfig(
            MaxRetries: 4,
            BaseDelay: TimeSpan.FromSeconds(1),
            MaxDelay: TimeSpan.FromSeconds(60),
            JitterMin: 0.1,
            JitterMax: 0.5);
        var limiter = new RateLimiter(config, new Random(42));

        for (var i = 0; i < 100; i++)
        {
            var delay = limiter.BackoffDelay(0, retryAfter: null);
            delay.TotalSeconds.Should().BeInRange(1.1, 1.5);
        }
    }

    [Fact]
    public void ShouldRetry_BelowMaxAttempts_TrueWhenErrorIsRetriable()
    {
        var limiter = new RateLimiter(ZeroJitter);

        limiter.ShouldRetry(0, new CloudflareApiError.Timeout()).Should().BeTrue();
        limiter.ShouldRetry(3, new CloudflareApiError.RateLimited(null)).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_AtOrAboveMaxAttempts_False()
    {
        var limiter = new RateLimiter(ZeroJitter);

        limiter.ShouldRetry(4, new CloudflareApiError.Timeout()).Should().BeFalse();
        limiter.ShouldRetry(10, new CloudflareApiError.Timeout()).Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_NonRetriableError_False()
    {
        var limiter = new RateLimiter(ZeroJitter);

        limiter.ShouldRetry(0, new CloudflareApiError.Unauthorized(TokenProblem.Invalid))
            .Should().BeFalse();
    }

    [Fact]
    public void Default_MaxRetriesIsFour()
    {
        RateLimiterConfig.Default.MaxRetries.Should().Be(4);
    }
}
