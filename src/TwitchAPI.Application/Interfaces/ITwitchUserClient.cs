using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Interfaces;

public interface ITwitchUserClient
{
    Task<TwitchUser?> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default);
}
