using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using FlarePurge.Core.Json;

namespace FlarePurge.Core.Api;

public sealed class ApiClient : IApiClient
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            CoreJsonContext.Default,
            new DefaultJsonTypeInfoResolver()),
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly RateLimiter _rateLimiter;
    private readonly Func<CancellationToken, ValueTask<string?>> _tokenProvider;

    public ApiClient(
        HttpClient http,
        RateLimiter rateLimiter,
        Func<CancellationToken, ValueTask<string?>> tokenProvider)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    public static ApiClient MakeDefault(Func<CancellationToken, ValueTask<string?>> tokenProvider)
    {
        var handler = new CertificatePinningHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = Endpoints.Base,
            Timeout = TimeSpan.FromSeconds(30),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FlarePurge/1.0 (Windows)");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return new ApiClient(http, new RateLimiter(), tokenProvider);
    }

    public Task<ApiResponse<T>> GetAsync<T>(
        string path,
        IReadOnlyList<(string Key, string Value)>? query = null,
        CancellationToken ct = default)
    {
        return ExecuteWithRetryAsync(async token =>
        {
            using var req = await BuildRequestAsync(HttpMethod.Get, path, query, body: null, bodyType: typeof(object), token).ConfigureAwait(false);
            return await SendAsync<T>(req, token).ConfigureAwait(false);
        }, ct);
    }

    public Task<ApiResponse<T>> PostAsync<TBody, T>(
        string path,
        TBody body,
        CancellationToken ct = default)
    {
        return ExecuteWithRetryAsync(async token =>
        {
            using var req = await BuildRequestAsync(HttpMethod.Post, path, query: null, body: body, bodyType: typeof(TBody), token).ConfigureAwait(false);
            return await SendAsync<T>(req, token).ConfigureAwait(false);
        }, ct);
    }

    private async Task<ApiResponse<T>> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<ApiResponse<T>>> send,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await send(ct).ConfigureAwait(false);
            }
            catch (CloudflareApiException ex) when (_rateLimiter.ShouldRetry(attempt, ex.Error))
            {
                var retryAfter = ex.Error switch
                {
                    CloudflareApiError.RateLimited r => r.RetryAfter,
                    _ => (TimeSpan?)null,
                };
                var delay = _rateLimiter.BackoffDelay(attempt, retryAfter);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<(string Key, string Value)>? query,
        object? body,
        Type bodyType,
        CancellationToken ct)
    {
        var uri = Endpoints.BuildUri(path, query);
        var request = new HttpRequestMessage(method, uri);

        var token = await _tokenProvider(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, bodyType, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new CloudflareApiException(new CloudflareApiError.Cancelled());
        }
        catch (TaskCanceledException)
        {
            throw new CloudflareApiException(new CloudflareApiError.Timeout());
        }
        catch (HttpRequestException ex) when (IsCertError(ex))
        {
            throw new CloudflareApiException(new CloudflareApiError.CertificatePinningFailed());
        }
        catch (HttpRequestException ex) when (IsNetworkError(ex))
        {
            throw new CloudflareApiException(new CloudflareApiError.NetworkUnavailable());
        }
        catch (HttpRequestException ex)
        {
            throw new CloudflareApiException(new CloudflareApiError.Unknown(ex.Message));
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await ReadSuccessAsync<T>(response, ct).ConfigureAwait(false);

            throw new CloudflareApiException(await MapErrorAsync(response, request, ct).ConfigureAwait(false));
        }
    }

    private static async Task<ApiResponse<T>> ReadSuccessAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(stream, JsonOptions, ct).ConfigureAwait(false);
            if (parsed is null)
                throw new CloudflareApiException(new CloudflareApiError.Decoding("empty response body"));
            return parsed;
        }
        catch (JsonException ex)
        {
            throw new CloudflareApiException(new CloudflareApiError.Decoding(ex.Message));
        }
    }

    private static async Task<CloudflareApiError> MapErrorAsync(HttpResponseMessage response, HttpRequestMessage request, CancellationToken ct)
    {
        CfErrorEnvelope? envelope = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            envelope = await JsonSerializer.DeserializeAsync<CfErrorEnvelope>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall through with a null envelope.
        }

        var status = (int)response.StatusCode;
        return status switch
        {
            401 => new CloudflareApiError.Unauthorized(MapTokenProblem(envelope?.FirstCode)),
            403 => new CloudflareApiError.Forbidden(null),
            404 => new CloudflareApiError.NotFound(request.RequestUri?.AbsolutePath ?? "unknown"),
            429 => new CloudflareApiError.RateLimited(ParseRetryAfter(response.Headers.RetryAfter)),
            >= 500 and < 600 => new CloudflareApiError.ServerError(status, envelope?.FirstCode, envelope?.FirstMessage),
            _ => new CloudflareApiError.Unknown($"HTTP {status}: {envelope?.FirstMessage ?? response.ReasonPhrase ?? ""}"),
        };
    }

    internal static TokenProblem MapTokenProblem(int? code) => code switch
    {
        10001 or 6003 => TokenProblem.Invalid,
        10000 => TokenProblem.Expired,
        _ => TokenProblem.Invalid,
    };

    internal static TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header is null) return null;
        if (header.Delta is { } delta) return delta;
        if (header.Date is { } date)
        {
            var diff = date - DateTimeOffset.UtcNow;
            return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
        }
        return null;
    }

    private static bool IsCertError(HttpRequestException ex)
        => ex.InnerException is AuthenticationException
        || ex.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase);

    private static bool IsNetworkError(HttpRequestException ex)
        => ex.InnerException is SocketException
        || ex.InnerException is IOException;
}
