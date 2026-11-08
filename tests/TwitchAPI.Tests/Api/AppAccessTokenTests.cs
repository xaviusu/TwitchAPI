using System.Net;
using Microsoft.Extensions.Logging;
using TwitchAPI.Application.Twitch;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class AppAccessTokenTests
{
    private const string UnauthorizedErrorJson =
        """{"error":"Unauthorized. Twitch access token is invalid or has expired."}""";

    private static readonly Uri StreamsUri = new("/analytics/streams", UriKind.Relative);

    [Fact]
    public async Task GetStreamsWhenCalledTwiceReturnsOkFromSingleTokenRequest()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var first = await client.GetAsync(StreamsUri);
        using var second = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(factory.Twitch.RequestsTo(FakeTwitch.TokenPath));
    }

    [Fact]
    public async Task GetStreamsWhenCachedTokenHasExpiredReturnsOkWithRenewedToken()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();
        using var warmUp = await client.GetAsync(StreamsUri);
        factory.Twitch.Respond(FakeTwitch.TokenPath, HttpStatusCode.OK, FakeTwitch.TokenJson("renewed-token"));
        factory.Time.Advance(FakeTwitch.DefaultTokenLifetime);

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, factory.Twitch.RequestsTo(FakeTwitch.TokenPath).Count);
        Assert.Equal("Bearer renewed-token", factory.Twitch.RequestsTo(FakeTwitch.StreamsPath)[^1].Headers["Authorization"]);
    }

    [Fact]
    public async Task GetStreamsWhenTokenIsWithinExpirySafetyMarginReturnsOkWithRenewedToken()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();
        using var warmUp = await client.GetAsync(StreamsUri);
        factory.Twitch.Respond(FakeTwitch.TokenPath, HttpStatusCode.OK, FakeTwitch.TokenJson("renewed-token"));
        factory.Time.Advance(FakeTwitch.DefaultTokenLifetime - TimeSpan.FromSeconds(30));

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, factory.Twitch.RequestsTo(FakeTwitch.TokenPath).Count);
        Assert.Equal("Bearer renewed-token", factory.Twitch.RequestsTo(FakeTwitch.StreamsPath)[^1].Headers["Authorization"]);
    }

    [Fact]
    public async Task GetStreamsWhenConcurrentRequestsFindExpiredTokenReturnsOkFromSingleRenewal()
    {
        // Arrange
        const int concurrentRequests = 10;
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();
        using var warmUp = await client.GetAsync(StreamsUri);
        var releaseToken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Twitch.HoldResponsesUntil(FakeTwitch.TokenPath, releaseToken.Task);
        factory.TokenWaiters.Expect(concurrentRequests);
        factory.Time.Advance(FakeTwitch.DefaultTokenLifetime);

        // Act
        var pending = Enumerable.Range(0, concurrentRequests).Select(_ => client.GetAsync(StreamsUri)).ToList();
        await factory.TokenWaiters.AllWaiting.WaitAsync(TimeSpan.FromSeconds(10));
        releaseToken.SetResult();
        var responses = await Task.WhenAll(pending);

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(2, factory.Twitch.RequestsTo(FakeTwitch.TokenPath).Count);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData("/analytics/streams", FakeTwitch.StreamsPath, HttpStatusCode.BadRequest)]
    [InlineData("/analytics/streams", FakeTwitch.StreamsPath, HttpStatusCode.Unauthorized)]
    [InlineData("/analytics/streams", FakeTwitch.StreamsPath, HttpStatusCode.Forbidden)]
    [InlineData("/analytics/user?id=141981764", FakeTwitch.UsersPath, HttpStatusCode.BadRequest)]
    [InlineData("/analytics/user?id=141981764", FakeTwitch.UsersPath, HttpStatusCode.Unauthorized)]
    [InlineData("/analytics/user?id=141981764", FakeTwitch.UsersPath, HttpStatusCode.Forbidden)]
    public async Task GetWhenTwitchRejectsClientCredentialsReturnsUnauthorizedError(
        string endpoint,
        string helixPath,
        HttpStatusCode tokenStatus)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.TokenPath, tokenStatus, """{"message":"client credentials rejected"}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(endpoint, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.Unauthorized, UnauthorizedErrorJson);
        Assert.Empty(factory.Twitch.RequestsTo(helixPath));
    }

    [Fact]
    public async Task GetStreamsWhenTwitchRejectsClientCredentialsReturnsUnauthorizedAndLogsRejectionCause()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.TokenPath, HttpStatusCode.Forbidden, """{"message":"client credentials rejected"}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(StreamsUri);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var log = Assert.Single(factory.Logs.GetSnapshot(), record => record.Exception is TwitchUnauthorizedException);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("403", log.Exception!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetWhenHelixRejectsTokenOnceReturnsOkFromRetryWithRenewedToken(
        string endpoint,
        string helixPath,
        string helixOkJson)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(helixPath, HttpStatusCode.OK, helixOkJson);
        factory.Twitch.RespondOnce(helixPath, HttpStatusCode.Unauthorized, FakeTwitch.HelixUnauthorizedJson);
        factory.Twitch.RespondOnce(FakeTwitch.TokenPath, HttpStatusCode.OK, FakeTwitch.TokenJson("rejected-token"));
        factory.Twitch.RespondOnce(FakeTwitch.TokenPath, HttpStatusCode.OK, FakeTwitch.TokenJson("renewed-token"));
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(endpoint, UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, factory.Twitch.RequestsTo(FakeTwitch.TokenPath).Count);
        var helixRequests = factory.Twitch.RequestsTo(helixPath);
        Assert.Equal(2, helixRequests.Count);
        Assert.Equal("Bearer rejected-token", helixRequests[0].Headers["Authorization"]);
        Assert.Equal("Bearer renewed-token", helixRequests[1].Headers["Authorization"]);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetWhenHelixRejectsRenewedTokenReturnsUnauthorizedErrorWithoutThirdAttempt(
        string endpoint,
        string helixPath,
        string helixOkJson)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(helixPath, HttpStatusCode.OK, helixOkJson);
        factory.Twitch.RespondOnce(helixPath, HttpStatusCode.Unauthorized, FakeTwitch.HelixUnauthorizedJson);
        factory.Twitch.RespondOnce(helixPath, HttpStatusCode.Unauthorized, FakeTwitch.HelixUnauthorizedJson);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(endpoint, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.Unauthorized, UnauthorizedErrorJson);
        Assert.Equal(2, factory.Twitch.RequestsTo(FakeTwitch.TokenPath).Count);
        Assert.Equal(2, factory.Twitch.RequestsTo(helixPath).Count);
    }

    public static TheoryData<string, string, string> Endpoints => new()
    {
        { "/analytics/streams", FakeTwitch.StreamsPath, """{"data":[{"title":"Title","user_name":"User"}],"pagination":{}}""" },
        { "/analytics/user?id=141981764", FakeTwitch.UsersPath, FakeTwitch.ExistingUserJson },
    };
}
