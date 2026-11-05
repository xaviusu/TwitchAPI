using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwitchAPI.Application.Exceptions;
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Models;

namespace TwitchAPI.Tests.Api;

public sealed class AnalyticsStreamsEndpointTests
{
    [Fact]
    public async Task GetStreamsAsyncReturns200WithExpectedContract()
    {
        // Arrange
        IReadOnlyList<TwitchStreamInfo> streams =
        [
            new("Title of Stream 1", "User1"),
            new("Title of Stream 2", "User2"),
            new("Title of Stream 3", "User3")
        ];

        using var factory = CreateFactory(
            () => Task.FromResult(streams));

        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync(
            "/analytics/streams");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(3, root.GetArrayLength());

        string[] expectedProperties = ["title", "user_name"];

        foreach (var item in root.EnumerateArray())
        {
            Assert.Equal(
                expectedProperties,
                item.EnumerateObject()
                    .Select(property => property.Name)
                    .ToArray());
        }

        Assert.Equal(
            "Title of Stream 1",
            root[0].GetProperty("title").GetString());
        Assert.Equal(
            "User1",
            root[0].GetProperty("user_name").GetString());
        Assert.Equal(
            "User3",
            root[2].GetProperty("user_name").GetString());
    }

    [Fact]
    public async Task GetStreamsAsyncWithNoLiveStreamsReturnsEmptyArray()
    {
        using var factory = CreateFactory(
            () => Task.FromResult<IReadOnlyList<TwitchStreamInfo>>([]));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/streams");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "[]",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetStreamsAsyncWhenTwitchAuthenticationFailsReturns401()
    {
        using var factory = CreateFactory(
            () => Task.FromException<IReadOnlyList<TwitchStreamInfo>>(
                new ExternalServiceException(
                    ExternalServiceErrorKind.AuthenticationFailed,
                    "Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/streams");

        await AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized. Twitch access token is invalid or has expired.");
    }

    [Theory]
    [InlineData(ExternalServiceErrorKind.Unavailable)]
    [InlineData(ExternalServiceErrorKind.Timeout)]
    [InlineData(ExternalServiceErrorKind.RateLimited)]
    public async Task GetStreamsAsyncWhenTwitchFailsReturns500(
        ExternalServiceErrorKind kind)
    {
        using var factory = CreateFactory(
            () => Task.FromException<IReadOnlyList<TwitchStreamInfo>>(
                new ExternalServiceException(kind, "Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/streams");

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Internal server error.");
    }

    [Fact]
    public async Task GetStreamsAsyncWhenUnexpectedErrorReturns500()
    {
        using var factory = CreateFactory(
            () => Task.FromException<IReadOnlyList<TwitchStreamInfo>>(
                new InvalidOperationException("Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/streams");

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Internal server error.");
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<Task<IReadOnlyList<TwitchStreamInfo>>> handler)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ITwitchStreamService>();

            services.AddSingleton<ITwitchStreamService>(
                new FakeTwitchStreamService(handler));
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedMessage)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Single(root.EnumerateObject());

        Assert.Equal(
            expectedMessage,
            root.GetProperty("error").GetString());
    }

    private sealed class FakeTwitchStreamService(
        Func<Task<IReadOnlyList<TwitchStreamInfo>>> handler)
        : ITwitchStreamService
    {
        public Task<IReadOnlyList<TwitchStreamInfo>> GetLiveStreamsAsync(
            CancellationToken cancellationToken = default)
        {
            return handler();
        }
    }
}
