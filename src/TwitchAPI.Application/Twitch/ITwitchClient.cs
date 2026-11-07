using TwitchAPI.Domain;

namespace TwitchAPI.Application.Twitch;

public interface ITwitchClient
{
    Task<IReadOnlyList<LiveStream>> GetLiveStreamsAsync(CancellationToken cancellationToken);
}
