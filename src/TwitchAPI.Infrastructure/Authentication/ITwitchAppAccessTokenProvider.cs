
namespace TwitchAPI.Infrastructure.Authentication;

public interface ITwitchAppAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        string rejectedToken,
        CancellationToken cancellationToken = default);
}
