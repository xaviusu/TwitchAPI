
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Services;

public sealed class TwitchUserService(
    ITwitchUserClient twitchUserClient) : ITwitchUserService
{
    public Task<TwitchUser?> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return twitchUserClient.GetUserAsync(
            id,
            cancellationToken);
    }
}
