using TwitchAPI.Domain;

namespace TwitchAPI.Application.Twitch;

public interface ITwitchClient
{
    Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(CancellationToken cancellationToken);

    Task<TwitchUser?> FindUserByIdAsync(string twitchUserId, CancellationToken cancellationToken);
}
