
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TwitchAPI.Infrastructure;
using TwitchAPI.Infrastructure.Authentication;

namespace TwitchAPI.Tests.Infrastructure;

public sealed class TwitchAppAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsyncCachesAndRegeneratesToken()
    {
        int requestCount = 0;

        using var client = CreateHttpClient((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/oauth2/token", request.RequestUri?.AbsolutePath);

            requestCount++;
            return Task.FromResult(CreateResponse(
                $"{{\"access_token\":\"token-{requestCount}\",\"expires_in\":3600}}"));
        });
        using var provider = CreateProvider(client);

        string first = await provider.GetAccessTokenAsync();
        string second = await provider.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, requestCount);

        await provider.InvalidateAsync(first);

        string third = await provider.GetAccessTokenAsync();

        Assert.Equal("token-2", third);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsyncCachesTokenUntilItsReportedExpiry()
    {
        int requestCount = 0;
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using var client = CreateHttpClient((_, _) =>
        {
            requestCount++;
            return Task.FromResult(CreateResponse(
                $"{{\"access_token\":\"token-{requestCount}\",\"expires_in\":30}}"));
        });
        using var provider = CreateProvider(client, timeProvider);

        string first = await provider.GetAccessTokenAsync();
        string beforeExpiry = await provider.GetAccessTokenAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        string afterExpiry = await provider.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal(first, beforeExpiry);
        Assert.Equal("token-2", afterExpiry);
        Assert.Equal(2, requestCount);
    }

    [Theory]
    [InlineData(3539, "token-1")]
    [InlineData(3540, "token-2")]
    public async Task GetAccessTokenAsyncRenewsTokenBeforeItExpires(
        int elapsedSeconds,
        string expectedToken)
    {
        int requestCount = 0;
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        using var client = CreateHttpClient((_, _) =>
        {
            requestCount++;
            return Task.FromResult(CreateResponse(
                $"{{\"access_token\":\"token-{requestCount}\",\"expires_in\":3600}}"));
        });
        using var provider = CreateProvider(client, timeProvider);

        await provider.GetAccessTokenAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        string token = await provider.GetAccessTokenAsync();

        Assert.Equal(expectedToken, token);
    }

    [Fact]
    public async Task GetAccessTokenAsyncMakesOnlyOneRequestForConcurrentCallers()
    {
        int requestCount = 0;
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var client = CreateHttpClient(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.TrySetResult();
            return await releaseResponse.Task.WaitAsync(cancellationToken);
        });
        using var provider = CreateProvider(client);

        Task<string>[] requests = Enumerable.Range(0, 8)
            .Select(_ => provider.GetAccessTokenAsync())
            .ToArray();

        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseResponse.SetResult(CreateResponse(
            "{\"access_token\":\"shared-token\",\"expires_in\":3600}"));

        string[] tokens = await Task.WhenAll(requests);

        Assert.All(tokens, token => Assert.Equal("shared-token", token));
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsyncCancellationWhileWaitingDoesNotBlockProvider()
    {
        int requestCount = 0;
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var client = CreateHttpClient(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.TrySetResult();
            return await releaseResponse.Task.WaitAsync(cancellationToken);
        });
        using var provider = CreateProvider(client);

        Task<string> firstRequest = provider.GetAccessTokenAsync();
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancellation = new CancellationTokenSource();
        Task<string> cancelledRequest = provider.GetAccessTokenAsync(
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelledRequest);

        releaseResponse.SetResult(CreateResponse(
            "{\"access_token\":\"shared-token\",\"expires_in\":3600}"));

        Assert.Equal("shared-token", await firstRequest);
        Assert.Equal("shared-token", await provider.GetAccessTokenAsync());
        Assert.Equal(1, requestCount);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"access_token\":42,\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":\"3600\"}")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":0}")]
    public async Task GetAccessTokenAsyncRejectsInvalidOAuthResponses(string json)
    {
        using var client = CreateHttpClient((_, _) =>
            Task.FromResult(CreateResponse(json)));
        using var provider = CreateProvider(client);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());

        Assert.Equal(
            "Twitch returned an invalid OAuth response.",
            exception.Message);
    }

    [Fact]
    public async Task GetAccessTokenAsyncCanRetryAfterHttpFailure()
    {
        int requestCount = 0;
        using var client = CreateHttpClient((_, _) =>
        {
            requestCount++;
            return Task.FromResult(requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : CreateResponse("{\"access_token\":\"token\",\"expires_in\":3600}"));
        });
        using var provider = CreateProvider(client);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.GetAccessTokenAsync());
        string token = await provider.GetAccessTokenAsync();

        Assert.Equal("token", token);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task AddInfrastructureValidatesOAuthCredentialsOnStartup()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services.AddInfrastructure(new ConfigurationBuilder().Build()))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());
    }

    private static TwitchAppAccessTokenProvider CreateProvider(
        HttpClient client,
        TimeProvider? timeProvider = null)
    {
        return new TwitchAppAccessTokenProvider(
            new StubHttpClientFactory(client),
            Options.Create(new TwitchOAuthOptions
            {
                ClientId = "test-client",
                ClientSecret = "test-secret"
            }),
            timeProvider ?? TimeProvider.System);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("https://id.twitch.tv/oauth2/")
        };
    }

    private static HttpResponseMessage CreateResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class StubHttpClientFactory(HttpClient client)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }
}
