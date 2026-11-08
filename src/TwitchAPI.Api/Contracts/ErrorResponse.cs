using System.Text.Json.Serialization;

namespace TwitchAPI.Api.Contracts;

public sealed record ErrorResponse([property: JsonPropertyName("error")] string Error)
{
    public static ErrorResponse InvalidId { get; } = new("Invalid or missing 'id' parameter.");

    public static ErrorResponse Unauthorized { get; } =
        new("Unauthorized. Twitch access token is invalid or has expired.");

    public static ErrorResponse UserNotFound { get; } = new("User not found.");

    public static ErrorResponse InternalServerError { get; } = new("Internal server error.");
}
