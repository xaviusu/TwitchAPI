
using Microsoft.AspNetCore.Mvc;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Api.Validation;
using TwitchAPI.Application.Interfaces;

namespace TwitchAPI.Api.Controllers;

[ApiController]
[Route("analytics")]
public sealed class AnalyticsController(
    ITwitchUserService twitchUserService,
    ITwitchStreamService twitchStreamService) : ControllerBase
{
    [HttpGet("user")]
    [ProducesResponseType(
        typeof(TwitchUserResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserAsync(
        [FromQuery(Name = "id")] string? id,
        CancellationToken cancellationToken)
    {
        if (!TwitchUserIdValidator.IsValid(id))
        {
            return BadRequest(
                new ApiErrorResponse(
                    "Invalid or missing 'id' parameter."));
        }

        var user = await twitchUserService.GetUserAsync(
            id!,
            cancellationToken);

        if (user is null)
        {
            return NotFound(
                new ApiErrorResponse("User not found."));
        }

        var response = new TwitchUserResponse(
            user.Id,
            user.Login,
            user.DisplayName,
            user.Type,
            user.BroadcasterType,
            user.Description,
            user.ProfileImageUrl,
            user.OfflineImageUrl,
            user.ViewCount,
            user.CreatedAt);

        return Ok(response);
    }

    [HttpGet("streams")]
    [ProducesResponseType(
        typeof(IReadOnlyList<TwitchStreamResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiErrorResponse),
        StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStreamsAsync(
        CancellationToken cancellationToken)
    {
        var streams = await twitchStreamService.GetLiveStreamsAsync(
            cancellationToken);

        var response = streams
            .Select(stream => new TwitchStreamResponse(
                stream.Title,
                stream.UserName))
            .ToList();

        return Ok(response);
    }
}
