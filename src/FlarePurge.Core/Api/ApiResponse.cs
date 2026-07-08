using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Api;

public sealed record ApiResponse<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("errors")] IReadOnlyList<ApiError> Errors,
    [property: JsonPropertyName("messages")] IReadOnlyList<ApiMessage> Messages,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("result_info")] ResultInfo? ResultInfo);

public sealed record ApiError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error_chain")] IReadOnlyList<ApiError>? ErrorChain);

public sealed record ApiMessage(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record ResultInfo(
    [property: JsonPropertyName("page")] int? Page,
    [property: JsonPropertyName("per_page")] int? PerPage,
    [property: JsonPropertyName("total_pages")] int? TotalPages,
    [property: JsonPropertyName("count")] int? Count,
    [property: JsonPropertyName("total_count")] int? TotalCount)
{
    public bool HasMorePages => Page.HasValue && TotalPages.HasValue && Page.Value < TotalPages.Value;
}
