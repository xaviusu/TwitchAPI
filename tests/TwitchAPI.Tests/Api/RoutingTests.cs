using System.Net;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class RoutingTests
{
    [Fact]
    public async Task GetWhenRouteDoesNotExistReturnsEmptyNotFound()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri("/analytics/unknown", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostWhenRouteOnlyAllowsGetReturnsEmptyMethodNotAllowed()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.PostAsync(new Uri("/analytics/streams", UriKind.Relative), content: null);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }
}
