using Microsoft.AspNetCore.Diagnostics;
using TwitchAPI.Api.Contracts;

namespace TwitchAPI.Api.ErrorHandling;

internal sealed partial class InternalServerErrorExceptionHandler(ILogger<InternalServerErrorExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnexpectedFailure(exception);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(ErrorResponse.InternalServerError, cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected failure while handling the request.")]
    private partial void LogUnexpectedFailure(Exception exception);
}
