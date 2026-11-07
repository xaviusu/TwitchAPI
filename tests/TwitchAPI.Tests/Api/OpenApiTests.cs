using System.Net;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class OpenApiTests
{
    [Fact]
    public async Task GetOpenApiDocumentWhenDevelopmentReturnsOk()
    {
        // Arrange
        using var factory = new TwitchApiFactory();
        using var client = factory.CreateApiClient();

        // Act
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
    }
}
