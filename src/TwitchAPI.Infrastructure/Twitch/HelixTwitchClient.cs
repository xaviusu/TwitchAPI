using System.Net;
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
        var page = await GetPageAsync<HelixStream>(LiveStreamsUri, cancellationToken);
        return [.. page.Data.Select(stream => new LiveStream(stream.Title, stream.UserName))];
    }

    public async Task<TwitchUser?> FindUserByIdAsync(TwitchUserId twitchUserId, CancellationToken cancellationToken)
    {
        var usersUri = new Uri($"users?id={Uri.EscapeDataString(twitchUserId.Value)}", UriKind.Relative);
        var page = await GetPageAsync<HelixUser>(usersUri, cancellationToken);
        return page.Data.Select(user => new TwitchUser(
            Id: user.Id,
            Login: user.Login,
            DisplayName: user.DisplayName,
            Type: user.Type,
            BroadcasterType: user.BroadcasterType,
            Description: user.Description,
            ProfileImageUrl: user.ProfileImageUrl,
            OfflineImageUrl: user.OfflineImageUrl,
            ViewCount: user.ViewCount,
            CreatedAt: user.CreatedAt)).FirstOrDefault();
    }

    private async Task<HelixPage<T>> GetPageAsync<T>(Uri relativeUri, CancellationToken cancellationToken)
    {
        using var response = await SendWithOneRenewalAsync(relativeUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HelixPage<T>>(cancellationToken)
            ?? throw new InvalidOperationException($"Twitch returned an empty response for {relativeUri}.");
    }

    private async Task<HttpResponseMessage> SendWithOneRenewalAsync(Uri relativeUri, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var response = await SendAsync(relativeUri, accessToken, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        tokenProvider.Invalidate(accessToken);
        var renewedAccessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var retryResponse = await SendAsync(relativeUri, renewedAccessToken, cancellationToken);
        if (retryResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return retryResponse;
        }

        retryResponse.Dispose();
        throw new TwitchUnauthorizedException($"Twitch rejected a renewed App Access Token for {relativeUri}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri relativeUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Add("Client-Id", options.Value.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private sealed record HelixPage<T>([property: JsonPropertyName("data")] IReadOnlyList<T> Data);

    private sealed record HelixStream(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("user_name")] string UserName);

    private sealed record HelixUser(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("broadcaster_type")] string BroadcasterType,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("profile_image_url")] string ProfileImageUrl,
        [property: JsonPropertyName("offline_image_url")] string OfflineImageUrl,
        [property: JsonPropertyName("view_count")] long ViewCount,
        [property: JsonPropertyName("created_at")] string CreatedAt);
}
