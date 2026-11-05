using System.Text.Json.Serialization;

namespace TwitchAPI.Infrastructure.Clients.Models;

internal sealed class TwitchStreamsEnvelope
{
    [JsonPropertyName("data")]
    public required List<TwitchStreamPayload> Data { get; init; }
}

internal sealed class TwitchStreamPayload
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("user_name")]
    public required string UserName { get; init; }
}
