using System.Text.Json.Serialization;

namespace TwitchAPI.Infrastructure.Authentication.Models;

internal sealed class TwitchOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public long? ExpiresIn { get; init; }
}
