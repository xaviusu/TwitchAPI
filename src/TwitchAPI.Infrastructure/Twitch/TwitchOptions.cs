using System.ComponentModel.DataAnnotations;

namespace TwitchAPI.Infrastructure.Twitch;

public sealed class TwitchOptions
{
    public const string SectionName = "Twitch";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public Uri AuthBaseAddress { get; set; } = new("https://id.twitch.tv/");

    [Required]
    public Uri HelixBaseAddress { get; set; } = new("https://api.twitch.tv/helix/");

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
