using System;
using System.Globalization;
using FlarePurge.Core.Localization;

namespace FlarePurge.Core.Api;

public abstract record CloudflareApiError
{
    public sealed record Unauthorized(TokenProblem Problem) : CloudflareApiError;
    public sealed record Forbidden(string? MissingScope) : CloudflareApiError;
    public sealed record RateLimited(TimeSpan? RetryAfter) : CloudflareApiError;
    public sealed record NotFound(string Resource) : CloudflareApiError;
    public sealed record NetworkUnavailable : CloudflareApiError;
    public sealed record Timeout : CloudflareApiError;
    public sealed record CertificatePinningFailed : CloudflareApiError;
    public sealed record Decoding(string Message) : CloudflareApiError;
    public sealed record ServerError(int StatusCode, int? CfCode, string? Message) : CloudflareApiError;
    public sealed record Cancelled : CloudflareApiError;
    public sealed record Unknown(string Detail) : CloudflareApiError;

    public string UserMessage => this switch
    {
        Unauthorized { Problem: TokenProblem.Invalid } => Localizer.Get("error.unauthorized.invalid"),
        Unauthorized { Problem: TokenProblem.Expired } => Localizer.Get("error.unauthorized.expired"),
        Unauthorized { Problem: TokenProblem.Revoked } => Localizer.Get("error.unauthorized.revoked"),
        Unauthorized => Localizer.Get("error.unauthorized.invalid"),
        Forbidden { MissingScope: { } scope } => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.forbidden.missingScope"),
            scope),
        Forbidden => Localizer.Get("error.forbidden.generic"),
        RateLimited { RetryAfter: { } retry } => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.rateLimited.withRetry"),
            (int)retry.TotalSeconds),
        RateLimited => Localizer.Get("error.rateLimited.generic"),
        NotFound notFound => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.notFound"),
            notFound.Resource),
        NetworkUnavailable => Localizer.Get("error.networkUnavailable"),
        Timeout => Localizer.Get("error.timeout"),
        CertificatePinningFailed => Localizer.Get("error.certPinning"),
        Decoding decoding => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.decoding"),
            decoding.Message),
        ServerError { Message: { } message } => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.server.withMessage"),
            message),
        ServerError => Localizer.Get("error.server"),
        Cancelled => Localizer.Get("error.cancelled"),
        Unknown unknown => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Get("error.unknown"),
            unknown.Detail),
        _ => Localizer.Get("error.unknown"),
    };

    public RecoveryAction Recovery => this switch
    {
        Unauthorized => RecoveryAction.Reauthenticate,
        Forbidden => RecoveryAction.Reauthenticate,
        RateLimited rate => new RecoveryAction.Retry(rate.RetryAfter),
        NotFound => RecoveryAction.RefreshList,
        NetworkUnavailable => RecoveryAction.CheckConnection,
        Timeout => new RecoveryAction.Retry(TimeSpan.FromSeconds(2)),
        CertificatePinningFailed => RecoveryAction.UpdateApp,
        _ => RecoveryAction.None,
    };

    public bool IsRetriable => this switch
    {
        RateLimited => true,
        Timeout => true,
        NetworkUnavailable => true,
        ServerError s => s.StatusCode >= 500 && s.StatusCode < 600,
        _ => false,
    };
}

public sealed class CloudflareApiException(CloudflareApiError error) : Exception(error.UserMessage)
{
    public CloudflareApiError Error { get; } = error;
}
