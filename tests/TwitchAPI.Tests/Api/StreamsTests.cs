using System.Net;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class StreamsTests
{
    private static readonly Uri StreamsUri = new("/analytics/streams", UriKind.Relative);

    [Fact]
    public async Task GetStreamsWhenTwitchHasLiveStreamsReturnsTitleAndUserNameInTwitchOrder()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.StreamsPath, HttpStatusCode.OK, """
            {
              "data": [
                { "id": "1", "user_id": "10", "user_login": "user1", "user_name": "User1", "game_name": "Chess",
                  "type": "live", "title": "Title of Stream 1", "viewer_count": 300, "language": "en" },
                { "id": "2", "user_id": "20", "user_login": "user2", "user_name": "User2", "game_name": "Go",
                  "type": "live", "title": "Title of Stream 2", "viewer_count": 200, "language": "es" }
              ],
              "pagination": { "cursor": "next-page" }
            }
            """);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.OK, """
            [
              { "title": "Title of Stream 1", "user_name": "User1" },
              { "title": "Title of Stream 2", "user_name": "User2" }
            ]
            """);
    }

    [Fact]
    public async Task GetStreamsWhenTwitchHasNoLiveStreamsReturnsEmptyArray()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.StreamsPath, HttpStatusCode.OK, """{"data":[],"pagination":{}}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.OK, "[]");
    }

    [Fact]
    public async Task GetStreamsWhenTwitchHasMorePagesReturnsOkFromSingleFirst100Request()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.StreamsPath, HttpStatusCode.OK, """
            {"data":[{"title":"Title","user_name":"User"}],"pagination":{"cursor":"next-page"}}
            """);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var helixRequest = Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.StreamsPath));
        Assert.Equal(HttpMethod.Get, helixRequest.Method);
        Assert.Equal("https://api.twitch.tv/helix/streams?first=100", helixRequest.Uri.ToString());
    }

    [Fact]
    public async Task GetStreamsWhenAppAccessTokenIsIssuedReturnsOkFromHelixCallWithClientIdAndBearerToken()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var helixRequest = Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.StreamsPath));
        Assert.Equal(TwitchApiFactory.ClientId, helixRequest.Headers["Client-Id"]);
        Assert.Equal($"Bearer {FakeTwitch.DefaultAccessToken}", helixRequest.Headers["Authorization"]);
    }

    [Fact]
    public async Task GetStreamsWhenClientCredentialsAreConfiguredReturnsOkAfterClientCredentialsGrant()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenRequest = Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.TokenPath));
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Equal("https://id.twitch.tv/oauth2/token", tokenRequest.Uri.ToString());
        Assert.Equal(
            $"client_id={TwitchApiFactory.ClientId}&client_secret={TwitchApiFactory.ClientSecret}&grant_type=client_credentials",
            tokenRequest.Body);
    }
}
