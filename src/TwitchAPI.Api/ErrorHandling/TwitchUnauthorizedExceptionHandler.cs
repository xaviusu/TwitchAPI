using Microsoft.AspNetCore.Diagnostics;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Application.Twitch;

namespace TwitchAPI.Api.ErrorHandling;

internal sealed partial class TwitchUnauthorizedExceptionHandler(ILogger<TwitchUnauthorizedExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TwitchUnauthorizedException)
        {
            return false;
        }

        LogTwitchUnauthorized(exception);
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await httpContext.Response.WriteAsJsonAsync(ErrorResponse.Unauthorized, cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Twitch rejected the App Access Token or the client credentials.")]
    private partial void LogTwitchUnauthorized(Exception exception);
}
