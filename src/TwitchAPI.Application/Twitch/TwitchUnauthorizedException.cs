namespace TwitchAPI.Application.Twitch;

public sealed class TwitchUnauthorizedException : Exception
{
    public TwitchUnauthorizedException()
    {
    }

    public TwitchUnauthorizedException(string message)
        : base(message)
    {
    }

    public TwitchUnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
