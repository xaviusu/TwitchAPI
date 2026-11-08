using System.Net;
using System.Text.Json.Nodes;

namespace TwitchAPI.Tests.Support;

public static class ResponseAssert
{
    public static async Task AssertJsonResponse(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedJson)
    {
        ArgumentNullException.ThrowIfNull(response);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var actualJson = await response.Content.ReadAsStringAsync();
        Assert.Equal(Normalize(expectedJson), Normalize(actualJson));
    }

    private static string? Normalize(string json) => JsonNode.Parse(json)?.ToJsonString();
}
