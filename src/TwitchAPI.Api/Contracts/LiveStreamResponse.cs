using System.Text.Json.Serialization;

namespace TwitchAPI.Api.Contracts;

public sealed record LiveStreamResponse(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("user_name")] string UserName);
