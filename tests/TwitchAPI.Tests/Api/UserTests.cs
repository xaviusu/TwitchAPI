using System.Net;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class UserTests
{
    private static readonly Uri UserUri = new("/analytics/user?id=141981764", UriKind.Relative);

    [Fact]
    public async Task GetUserWhenTwitchKnowsIdReturnsUserFieldsInSpecOrder()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.UsersPath, HttpStatusCode.OK, FakeTwitch.ExistingUserJson);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(UserUri);

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.OK, """
            {
              "id": "141981764",
              "login": "twitchdev",
              "display_name": "TwitchDev",
              "type": "",
              "broadcaster_type": "partner",
              "description": "Supporting third-party developers building Twitch integrations.",
              "profile_image_url": "https://static-cdn.jtvnw.net/profile.png",
              "offline_image_url": "",
              "view_count": 5980557,
              "created_at": "2016-12-14T20:32:28Z"
            }
            """);
    }

    [Fact]
    public async Task GetUserWhenTwitchKnowsIdReturnsOkFromSingleGetUsersRequestById()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.UsersPath, HttpStatusCode.OK, FakeTwitch.ExistingUserJson);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(UserUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var helixRequest = Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.UsersPath));
        Assert.Equal(HttpMethod.Get, helixRequest.Method);
        Assert.Equal("https://api.twitch.tv/helix/users?id=141981764", helixRequest.Uri.ToString());
        Assert.Equal(TwitchApiFactory.ClientId, helixRequest.Headers["Client-Id"]);
        Assert.Equal($"Bearer {FakeTwitch.DefaultAccessToken}", helixRequest.Headers["Authorization"]);
    }

    [Fact]
    public async Task GetUserWhenTwitchReturnsEmptyDataReturnsNotFoundError()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.UsersPath, HttpStatusCode.OK, """{"data":[]}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(UserUri);

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.NotFound, """{"error":"User not found."}""");
    }
}
