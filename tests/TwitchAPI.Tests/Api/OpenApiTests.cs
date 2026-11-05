
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TwitchAPI.Tests.Api;

public sealed class OpenApiTests
{
    [Fact]
    public async Task OpenApiEndpointReturnsSuccess()
    {
        // Arrange
        using var factory = new TestWebApplicationFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        // Act
        using var response = await client.GetAsync(
            "/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
    }
}
