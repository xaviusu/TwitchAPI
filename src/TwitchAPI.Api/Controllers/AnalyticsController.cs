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
}
