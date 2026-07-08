using System;

namespace FlarePurge.Core.Api;

public sealed class RateLimiter
{
    private readonly RateLimiterConfig _config;
    private readonly Random _rng;

    public RateLimiter(RateLimiterConfig? config = null, Random? random = null)
    {
        _config = config ?? RateLimiterConfig.Default;
        _rng = random ?? Random.Shared;
    }

    public TimeSpan BackoffDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } ra)
            return ra < _config.MaxDelay ? ra : _config.MaxDelay;

        var exp = _config.BaseDelay.TotalSeconds * Math.Pow(2, attempt);
        var jitter = _rng.NextDouble() * (_config.JitterMax - _config.JitterMin) + _config.JitterMin;
        var seconds = Math.Min(exp + jitter, _config.MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    public bool ShouldRetry(int attempt, CloudflareApiError error)
        => attempt < _config.MaxRetries && error.IsRetriable;
}

public sealed record RateLimiterConfig(
    int MaxRetries,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    double JitterMin,
    double JitterMax)
{
    public static readonly RateLimiterConfig Default = new(
        MaxRetries: 4,
        BaseDelay: TimeSpan.FromSeconds(1),
        MaxDelay: TimeSpan.FromSeconds(60),
        JitterMin: 0.0,
        JitterMax: 0.5);
}
