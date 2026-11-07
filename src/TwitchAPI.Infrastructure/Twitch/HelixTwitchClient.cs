using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Twitch;
using TwitchAPI.Domain;

namespace TwitchAPI.Infrastructure.Twitch;

internal sealed class HelixTwitchClient(
    HttpClient httpClient,
    IAppAccessTokenProvider tokenProvider,
    IOptions<TwitchOptions> options) : ITwitchClient
{
    private static readonly Uri LiveStreamsUri = new("streams?first=100", UriKind.Relative);

    public async Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, LiveStreamsUri);
        request.Headers.Add("Client-Id", options.Value.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<HelixPage<HelixStream>>(cancellationToken)
            ?? throw new InvalidOperationException("Twitch returned an empty streams response.");
        return [.. page.Data.Select(stream => new LiveStream(stream.Title, stream.UserName))];
    }

    private sealed record HelixPage<T>([property: JsonPropertyName("data")] IReadOnlyList<T> Data);

    private sealed record HelixStream(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("user_name")] string UserName);
}
