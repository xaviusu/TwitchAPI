using System.Text.Json.Serialization;

namespace TwitchAPI.Api.Contracts;

public sealed record ErrorResponse([property: JsonPropertyName("error")] string Error);
