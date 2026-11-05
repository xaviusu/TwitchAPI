using System.Text.Json;
using TwitchAPI.Api.Contracts;

namespace TwitchAPI.Tests.Api;

public sealed class TwitchUserResponseTests
{
    [Fact]
    public void SerializeProducesExpectedJsonContract()
    {
        // Arrange
        var response = new TwitchUserResponse(
            Id: "12345678",
            Login: "user_login",
            DisplayName: "User Display Name",
            Type: "",
            BroadcasterType: "partner",
            Description: "This is a generic description.",
            ProfileImageUrl: "https://example.com/profile.png",
            OfflineImageUrl: "https://example.com/offline.jpeg",
            ViewCount: 0,
            CreatedAt: "2020-01-01T00:00:00Z"
        );

        // Act
        string json = JsonSerializer.Serialize(response);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;

        // Assert
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

        string[] actualProperties = root
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(expectedProperties, actualProperties);

        Assert.Equal(
            "User Display Name",
            root.GetProperty("display_name").GetString());

        Assert.Equal(
            0,
            root.GetProperty("view_count").GetInt32());

        Assert.Equal(
            "2020-01-01T00:00:00Z",
            root.GetProperty("created_at").GetString());
    }
}
