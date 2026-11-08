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

    [Fact]
    public async Task GetUserWhenIdHasTwentyDigitsForwardsIdToTwitch()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri("/analytics/user?id=12345678901234567890", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var helixRequest = Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.UsersPath));
        Assert.Equal("https://api.twitch.tv/helix/users?id=12345678901234567890", helixRequest.Uri.ToString());
    }

    [Theory]
    [InlineData("/analytics/user")]
    [InlineData("/analytics/user?id=")]
    [InlineData("/analytics/user?id=abc")]
    [InlineData("/analytics/user?id=12a")]
    [InlineData("/analytics/user?id=-5")]
    [InlineData("/analytics/user?id=%2B5")]
    [InlineData("/analytics/user?id=+5")]
    [InlineData("/analytics/user?id=%2012")]
    [InlineData("/analytics/user?id=12%20")]
    [InlineData("/analytics/user?id=%EF%BC%91%EF%BC%92")]
    [InlineData("/analytics/user?id=123456789012345678901")]
    [InlineData("/analytics/user?id=1&id=2")]
    [InlineData("/analytics/user?id=1&id=1")]
    public async Task GetUserWhenIdIsInvalidReturnsBadRequestWithoutCallingTwitch(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(
            response,
            HttpStatusCode.BadRequest,
            """{"error":"Invalid or missing 'id' parameter."}""");
        Assert.Empty(factory.Twitch.RequestsTo(FakeTwitch.UsersPath));
        Assert.Empty(factory.Twitch.RequestsTo(FakeTwitch.TokenPath));
    }
}
