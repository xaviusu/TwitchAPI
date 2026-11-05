namespace TwitchAPI.Infrastructure.Authentication;

public sealed class TwitchOAuthOptions
{
    public const string SectionName = "Twitch";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
