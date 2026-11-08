namespace TwitchAPI.Application.Twitch;

public interface IAppAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    void Invalidate(string rejectedAccessToken);
}
