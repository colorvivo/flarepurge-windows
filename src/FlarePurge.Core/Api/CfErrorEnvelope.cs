using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlarePurge.Core.Api;

internal sealed record CfErrorEnvelope(
    [property: JsonPropertyName("errors")] IReadOnlyList<ApiError>? Errors)
{
    public int? FirstCode => Errors is { Count: > 0 } e ? e[0].Code : null;
    public string? FirstMessage => Errors is { Count: > 0 } e ? e[0].Message : null;
}
