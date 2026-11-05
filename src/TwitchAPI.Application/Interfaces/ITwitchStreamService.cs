using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Interfaces;

public interface ITwitchStreamService
{
    Task<IReadOnlyList<TwitchStreamInfo>> GetLiveStreamsAsync(
        CancellationToken cancellationToken = default);
}
