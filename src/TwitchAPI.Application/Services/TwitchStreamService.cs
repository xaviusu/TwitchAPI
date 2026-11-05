using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Services;

public sealed class TwitchStreamService(
    ITwitchStreamClient twitchStreamClient) : ITwitchStreamService
{
    public Task<IReadOnlyList<TwitchStreamInfo>> GetLiveStreamsAsync(
        CancellationToken cancellationToken = default)
    {
        return twitchStreamClient.GetLiveStreamsAsync(
            cancellationToken);
    }
}
