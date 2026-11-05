using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Interfaces;

public interface ITwitchStreamClient
{
    Task<IReadOnlyList<TwitchStreamInfo>> GetLiveStreamsAsync(
        CancellationToken cancellationToken = default);
}
