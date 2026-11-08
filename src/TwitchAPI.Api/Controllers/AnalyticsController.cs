using Microsoft.AspNetCore.Mvc;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Application.Twitch;

namespace TwitchAPI.Api.Controllers;

[ApiController]
[Route("analytics")]
public sealed class AnalyticsController(ITwitchClient twitchClient) : ControllerBase
{
    [HttpGet("streams")]
    [ProducesResponseType<IReadOnlyList<LiveStreamResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LiveStreamResponse>>> GetStreams(CancellationToken cancellationToken)
    {
        var liveStreams = await twitchClient.GetLiveStreamsAsync(cancellationToken);
        return Ok(liveStreams.Select(stream => new LiveStreamResponse(stream.Title, stream.UserName)).ToList());
    }

    [HttpGet("user")]
    [ProducesResponseType<TwitchUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwitchUserResponse>> GetUser(
        [FromQuery] string id,
        CancellationToken cancellationToken)
    {
        var user = await twitchClient.FindUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(new ErrorResponse("User not found."));
        }

        return Ok(new TwitchUserResponse(
            Id: user.Id,
            Login: user.Login,
            DisplayName: user.DisplayName,
            Type: user.Type,
            BroadcasterType: user.BroadcasterType,
            Description: user.Description,
            ProfileImageUrl: user.ProfileImageUrl,
            OfflineImageUrl: user.OfflineImageUrl,
            ViewCount: user.ViewCount,
            CreatedAt: user.CreatedAt));
    }
}
