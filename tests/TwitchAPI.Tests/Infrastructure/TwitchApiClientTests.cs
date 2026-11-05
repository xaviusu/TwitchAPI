
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Exceptions;
using TwitchAPI.Infrastructure.Authentication;
using TwitchAPI.Infrastructure.Clients;

namespace TwitchAPI.Tests.Infrastructure;

public sealed class TwitchApiClientTests
{
    private const string UserResponse = """
        {
          "data": [
            {
              "id": "1234",
              "login": "test_user",
              "display_name": "Test User",
              "type": "",
              "broadcaster_type": "partner",
              "description": "Test description",
              "profile_image_url": "https://example.com/profile.png",
              "offline_image_url": "https://example.com/offline.png",
              "view_count": 0,
              "created_at": "2020-01-01T00:00:00Z"
            }
          ]
        }
        """;

    [Fact]
    public async Task GetUserAsyncWithValidUserReturnsUser()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                Assert.Equal(
                    "/helix/users?id=1234",
                    request.RequestUri?.PathAndQuery);

                Assert.Equal(
                    "Bearer",
                    request.Headers.Authorization?.Scheme);

                Assert.Equal(
                    "first-token",
                    request.Headers.Authorization?.Parameter);

                Assert.Equal(
                    "test-client",
                    request.Headers.GetValues("Client-Id").Single());

                return JsonResponse(UserResponse);
            }))
        {
            BaseAddress = new Uri(
                "https://api.twitch.tv/helix/")
        };

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var user = await client.GetUserAsync("1234");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("1234", user.Id);
        Assert.Equal("test_user", user.Login);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal("partner", user.BroadcasterType);
        Assert.Equal(0, user.ViewCount);
    }

    [Fact]
    public async Task GetUserAsyncWithUnknownUserReturnsNull()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(_ =>
                JsonResponse("""{"data":[]}""")))
        {
            BaseAddress = new Uri(
                "https://api.twitch.tv/helix/")
        };

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var user = await client.GetUserAsync("9999");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserAsyncWhenUnauthorizedRetriesOnce()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();
        int requestCount = 0;

        using var httpClient = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                requestCount++;

                string? token =
                    request.Headers.Authorization?.Parameter;

                if (requestCount == 1)
                {
                    Assert.Equal("first-token", token);

                    return new HttpResponseMessage(
                        HttpStatusCode.Unauthorized);
                }

                Assert.Equal("renewed-token", token);

                return JsonResponse(UserResponse);
            }))
        {
            BaseAddress = new Uri(
                "https://api.twitch.tv/helix/")
        };

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var user = await client.GetUserAsync("1234");

        // Assert
        Assert.NotNull(user);
        Assert.Equal(2, requestCount);
        Assert.Equal(1, tokenProvider.InvalidationCount);
    }

    [Fact]
    public async Task GetUserAsyncWhenRetryFailsThrowsAuthenticationFailed()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = CreateHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.AuthenticationFailed,
            exception.Kind);
    }

    [Fact]
    public async Task GetUserAsyncWhenCredentialsRejectedThrowsAuthenticationFailed()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider
        {
            TokenException = new HttpRequestException(
                "Unauthorized",
                null,
                HttpStatusCode.Unauthorized)
        };

        using var httpClient = CreateHttpClient(_ =>
            throw new InvalidOperationException(
                "Helix should not be called."));

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.AuthenticationFailed,
            exception.Kind);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetUserAsyncWhenTwitchFailsThrowsUnavailable()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = CreateHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.Unavailable,
            exception.Kind);

        var inner = Assert.IsType<HttpRequestException>(
            exception.InnerException);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            inner.StatusCode);
    }

    [Fact]
    public async Task GetUserAsyncWhenRateLimitedThrowsRateLimited()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = CreateHttpClient(_ =>
        {
            var response = new HttpResponseMessage(
                HttpStatusCode.TooManyRequests);

            response.Headers.RetryAfter =
                new RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(5));

            return response;
        });

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.RateLimited,
            exception.Kind);
        Assert.Equal(
            TimeSpan.FromSeconds(5),
            exception.RetryAfter);
    }

    [Fact]
    public async Task GetUserAsyncWhenTwitchTimesOutThrowsTimeout()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = CreateHttpClient(_ =>
            throw new TaskCanceledException(
                "Simulated HttpClient timeout.",
                new TimeoutException()));

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.Timeout,
            exception.Kind);
    }

    [Fact]
    public async Task GetUserAsyncWhenCallerCancelsPropagatesCancellation()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        using var httpClient = CreateHttpClient(_ =>
            throw new OperationCanceledException(cancellation.Token));

        var client = CreateClient(httpClient, tokenProvider);

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetUserAsync("1234", cancellation.Token));
    }

    [Fact]
    public async Task GetUserAsyncWithMalformedResponseThrowsUnavailable()
    {
        // Arrange
        var tokenProvider = new FakeTokenProvider();

        using var httpClient = CreateHttpClient(_ =>
            JsonResponse("""{"data":[{"id":"1234"}]}"""));

        var client = CreateClient(httpClient, tokenProvider);

        // Act
        var exception = await Assert.ThrowsAsync<
            ExternalServiceException>(
            () => client.GetUserAsync("1234"));

        // Assert
        Assert.Equal(
            ExternalServiceErrorKind.Unavailable,
            exception.Kind);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/")
        };
    }

    private static TwitchApiClient CreateClient(
        HttpClient httpClient,
        ITwitchAppAccessTokenProvider tokenProvider)
    {
        return new TwitchApiClient(
            httpClient,
            tokenProvider,
            Options.Create(new TwitchOAuthOptions
            {
                ClientId = "test-client",
                ClientSecret = "test-secret"
            }));
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class FakeTokenProvider
        : ITwitchAppAccessTokenProvider
    {
        private string _token = "first-token";

        public int InvalidationCount { get; private set; }

        public Exception? TokenException { get; init; }

        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken = default)
        {
            return TokenException is null
                ? Task.FromResult(_token)
                : Task.FromException<string>(TokenException);
        }

        public Task InvalidateAsync(
            string rejectedToken,
            CancellationToken cancellationToken = default)
        {
            InvalidationCount++;
            _token = "renewed-token";

            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
