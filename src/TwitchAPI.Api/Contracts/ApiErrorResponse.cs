
using System.Text.Json.Serialization;

namespace TwitchAPI.Api.Contracts;

public sealed record ApiErrorResponse(
    [property: JsonPropertyName("error")]
    string Error);
