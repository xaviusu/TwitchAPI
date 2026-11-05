
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwitchAPI.Application.Exceptions;
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Models;

namespace TwitchAPI.Tests.Api;

public sealed class AnalyticsEndpointTests
{
    [Fact]
    public async Task GetUserAsyncWhenUserExistsReturns200()
    {
        // Arrange
        var user = new TwitchUser(
            "1234",
            "test_user",
            "Test User",
            "",
            "partner",
            "Description",
            "https://example.com/profile.png",
            "https://example.com/offline.png",
            0,
            "2020-01-01T00:00:00Z");

        using var factory = CreateFactory(
            _ => Task.FromResult<TwitchUser?>(user));

        using var client = CreateClient(factory);

        // Act
        using var response = await client.GetAsync(
            "/analytics/user?id=1234");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        string[] expectedProperties =
        [
            "id",
            "login",
            "display_name",
            "type",
            "broadcaster_type",
            "description",
            "profile_image_url",
            "offline_image_url",
            "view_count",
            "created_at"
        ];

        Assert.Equal(
            expectedProperties,
            root.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());

        Assert.Equal("1234", root.GetProperty("id").GetString());
        Assert.Equal(
            "test_user",
            root.GetProperty("login").GetString());
        Assert.Equal(
            "Test User",
            root.GetProperty("display_name").GetString());
        Assert.Equal(
            0,
            root.GetProperty("view_count").GetInt32());
    }

    [Theory]
    [InlineData("/analytics/user")]
    [InlineData("/analytics/user?id=")]
    [InlineData("/analytics/user?id=invalid")]
    [InlineData("/analytics/user?id=-123")]
    public async Task GetUserAsyncWhenIdInvalidReturns400(
        string endpoint)
    {
        using var factory = CreateFactory(
            _ => throw new InvalidOperationException(
                "The service should not be called."));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(endpoint);

        await AssertErrorAsync(
            response,
            HttpStatusCode.BadRequest,
            "Invalid or missing 'id' parameter.");
    }

    [Fact]
    public async Task GetUserAsyncWhenUserMissingReturns404()
    {
        using var factory = CreateFactory(
            _ => Task.FromResult<TwitchUser?>(null));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/user?id=9999");

        await AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "User not found.");
    }

    [Fact]
    public async Task GetUserAsyncWhenTwitchAuthenticationFailsReturns401()
    {
        using var factory = CreateFactory(
            _ => Task.FromException<TwitchUser?>(
                new ExternalServiceException(
                    ExternalServiceErrorKind.AuthenticationFailed,
                    "Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/user?id=1234");

        await AssertErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized. Twitch access token is invalid or has expired.");
    }

    [Theory]
    [InlineData(ExternalServiceErrorKind.Unavailable)]
    [InlineData(ExternalServiceErrorKind.Timeout)]
    [InlineData(ExternalServiceErrorKind.RateLimited)]
    public async Task GetUserAsyncWhenTwitchFailsReturns500(
        ExternalServiceErrorKind kind)
    {
        using var factory = CreateFactory(
            _ => Task.FromException<TwitchUser?>(
                new ExternalServiceException(kind, "Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/user?id=1234");

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Internal server error.");
    }

    [Fact]
    public async Task UnknownRouteReturns404WithErrorBody()
    {
        using var factory = CreateFactory(
            _ => throw new InvalidOperationException(
                "The service should not be called."));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/does-not-exist");

        await AssertErrorAsync(
            response,
            HttpStatusCode.NotFound,
            "Not Found");
    }

    [Fact]
    public async Task GetUserAsyncWhenUnexpectedErrorReturns500()
    {
        using var factory = CreateFactory(
            _ => Task.FromException<TwitchUser?>(
                new InvalidOperationException("Simulated failure.")));

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/analytics/user?id=1234");

        await AssertErrorAsync(
            response,
            HttpStatusCode.InternalServerError,
            "Internal server error.");
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<string, Task<TwitchUser?>> handler)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ITwitchUserService>();

            services.AddSingleton<ITwitchUserService>(
                new FakeTwitchUserService(handler));
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

        Assert.Single(
            root.EnumerateObject());

        Assert.Equal(
            expectedMessage,
            root.GetProperty("error").GetString());
    }

    private sealed class FakeTwitchUserService(
        Func<string, Task<TwitchUser?>> handler)
        : ITwitchUserService
    {
        public Task<TwitchUser?> GetUserAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            return handler(id);
        }
    }
}
