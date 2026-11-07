using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Twitch;

namespace TwitchAPI.Infrastructure.Twitch;

internal sealed class AppAccessTokenProvider(HttpClient httpClient, IOptions<TwitchOptions> options)
    : IAppAccessTokenProvider
{
    private static readonly Uri TokenUri = new("oauth2/token", UriKind.Relative);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.Value.ClientId,
            ["client_secret"] = options.Value.ClientSecret,
            ["grant_type"] = "client_credentials",
        });
        using var response = await httpClient.PostAsync(TokenUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Twitch returned an empty token response.");
        return token.AccessToken;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}
