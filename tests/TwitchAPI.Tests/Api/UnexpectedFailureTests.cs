using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TwitchAPI.Application.Twitch;
using TwitchAPI.Domain;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class UnexpectedFailureTests
{
    private const string InternalServerErrorJson = """{"error":"Internal server error."}""";
    private const string StreamsUri = "/analytics/streams";
    private const string UserUri = "/analytics/user?id=141981764";

    [Theory]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath, HttpStatusCode.TooManyRequests)]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath, HttpStatusCode.InternalServerError)]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath, HttpStatusCode.ServiceUnavailable)]
    [InlineData(UserUri, FakeTwitch.UsersPath, HttpStatusCode.TooManyRequests)]
    [InlineData(UserUri, FakeTwitch.UsersPath, HttpStatusCode.InternalServerError)]
    [InlineData(UserUri, FakeTwitch.UsersPath, HttpStatusCode.ServiceUnavailable)]
    public async Task GetWhenHelixFailsReturnsInternalServerError(string uri, string helixPath, HttpStatusCode helixStatus)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(helixPath, helixStatus, $$"""{"status":{{(int)helixStatus}}}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData("""{"data":[{"user_name":"User"}]}""")]
    [InlineData("""{"data":[{"title":"Title"}]}""")]
    [InlineData("""{"data":[{"title":null,"user_name":"User"}]}""")]
    [InlineData("""{"data":[{"title":"Title","user_name":null}]}""")]
    [InlineData("""{"pagination":{}}""")]
    [InlineData("""{"data":null}""")]
    public async Task GetStreamsWhenHelixOmitsRequiredFieldsReturnsInternalServerError(string helixJson)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.StreamsPath, HttpStatusCode.OK, helixJson);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(StreamsUri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath)]
    [InlineData(UserUri, FakeTwitch.UsersPath)]
    public async Task GetWhenHelixReturnsInvalidJsonReturnsInternalServerError(string uri, string helixPath)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(helixPath, HttpStatusCode.OK, "<html>not json</html>");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Fact]
    public async Task GetUserWhenHelixReturnsBadRequestForWellFormedIdReturnsInternalServerError()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(
            FakeTwitch.UsersPath,
            HttpStatusCode.BadRequest,
            """{"error":"Bad Request","status":400,"message":"Invalid id"}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(UserUri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri)]
    [InlineData(UserUri)]
    public async Task GetWhenTokenEndpointReturnsServerErrorReturnsInternalServerError(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.TokenPath, HttpStatusCode.ServiceUnavailable, """{"status":503}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
        Assert.Empty(factory.Twitch.RequestsTo(FakeTwitch.StreamsPath));
        Assert.Empty(factory.Twitch.RequestsTo(FakeTwitch.UsersPath));
    }

    [Theory]
    [InlineData(StreamsUri)]
    [InlineData(UserUri)]
    public async Task GetWhenTokenEndpointIsUnreachableReturnsInternalServerError(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.FailWith(FakeTwitch.TokenPath, new HttpRequestException("Connection refused."));
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri)]
    [InlineData(UserUri)]
    public async Task GetWhenTokenEndpointReturnsInvalidJsonReturnsInternalServerError(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.TokenPath, HttpStatusCode.OK, "<html>not json</html>");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri)]
    [InlineData(UserUri)]
    public async Task GetWhenTokenEndpointExceedsConfiguredTimeoutReturnsInternalServerError(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { ["Twitch:Timeout"] = "00:00:00.050" });
        var neverReleased = new TaskCompletionSource();
        factory.Twitch.HoldResponsesUntil(FakeTwitch.TokenPath, neverReleased.Task);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath)]
    [InlineData(UserUri, FakeTwitch.UsersPath)]
    public async Task GetWhenHelixExceedsConfiguredTimeoutReturnsInternalServerError(string uri, string helixPath)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { ["Twitch:Timeout"] = "00:00:00.050" });
        var neverReleased = new TaskCompletionSource();
        factory.Twitch.HoldResponsesUntil(helixPath, neverReleased.Task);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    [Theory]
    [InlineData(StreamsUri, FakeTwitch.StreamsPath)]
    [InlineData(UserUri, FakeTwitch.UsersPath)]
    public async Task GetWhenHelixFailsReturnsInternalServerErrorAfterSingleHelixRequest(string uri, string helixPath)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(helixPath, HttpStatusCode.ServiceUnavailable, """{"status":503}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(factory.Twitch.RequestsTo(helixPath));
    }

    [Fact]
    public async Task GetStreamsWhenHelixFailsReturnsInternalServerErrorAndLogsRealCause()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.StreamsPath, HttpStatusCode.ServiceUnavailable, """{"status":503}""");
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(StreamsUri, UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var log = Assert.Single(factory.Logs.GetSnapshot(), record => record.Exception is HttpRequestException);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ((HttpRequestException)log.Exception!).StatusCode);
    }

    [Theory]
    [InlineData(StreamsUri)]
    [InlineData(UserUri)]
    public async Task GetWhenApiThrowsUnhandledExceptionReturnsInternalServerErrorAndLogsIt(string uri)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var throwingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITwitchClient>();
            services.AddSingleton<ITwitchClient, ThrowingTwitchClient>();
        }));
        using var client = throwingFactory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
        var log = Assert.Single(
            throwingFactory.Services.GetFakeLogCollector().GetSnapshot(),
            record => record.Exception is InvalidOperationException);
        Assert.Equal(LogLevel.Error, log.Level);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"data":[{"id":"141981764","login":"twitchdev"}]}""")]
    [InlineData("""{"data":null}""")]
    [InlineData("""
        {"data":[{"id":"141981764","login":"twitchdev","display_name":null,"type":"","broadcaster_type":"partner",
        "description":"","profile_image_url":"","offline_image_url":"","view_count":0,"created_at":"2016-12-14T20:32:28Z"}]}
        """)]
    [InlineData("""
        {"data":[{"id":"141981764","login":"twitchdev","display_name":"TwitchDev","type":"","broadcaster_type":"partner",
        "description":"","profile_image_url":"","offline_image_url":"","view_count":null,"created_at":"2016-12-14T20:32:28Z"}]}
        """)]
    public async Task GetUserWhenHelixOmitsRequiredFieldsReturnsInternalServerError(string helixJson)
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        factory.Twitch.Respond(FakeTwitch.UsersPath, HttpStatusCode.OK, helixJson);
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri(UserUri, UriKind.Relative));

        // Assert
        await ResponseAssert.AssertJsonResponse(response, HttpStatusCode.InternalServerError, InternalServerErrorJson);
    }

    private sealed class ThrowingTwitchClient : ITwitchClient
    {
        public Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected API failure.");

        public Task<TwitchUser?> FindUserByIdAsync(TwitchUserId twitchUserId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unexpected API failure.");
    }
}
