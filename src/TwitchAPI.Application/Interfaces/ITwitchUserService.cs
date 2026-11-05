
using TwitchAPI.Application.Models;

namespace TwitchAPI.Application.Interfaces;

public interface ITwitchUserService
{
    Task<TwitchUser?> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default);
}
