using System.Text.Json;

namespace TwitchAPI.Infrastructure.Twitch;

internal static class TwitchJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };
}
