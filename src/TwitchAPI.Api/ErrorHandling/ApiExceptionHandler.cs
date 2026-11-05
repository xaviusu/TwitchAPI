using Microsoft.AspNetCore.Diagnostics;
using TwitchAPI.Api.Contracts;
using TwitchAPI.Application.Exceptions;

namespace TwitchAPI.Api.ErrorHandling;

public sealed partial class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger);
            httpContext.Response.StatusCode =
                StatusCodes.Status499ClientClosedRequest;

            return true;
        }

        int statusCode;
        string message;

        if (exception is ExternalServiceException
            {
                Kind: ExternalServiceErrorKind.AuthenticationFailed
            } authenticationFailure)
        {
            statusCode = StatusCodes.Status401Unauthorized;
            message = "Unauthorized. Twitch access token is invalid or has expired.";
            LogTwitchAuthenticationFailure(logger, authenticationFailure);
        }
        else if (exception is ExternalServiceException externalFailure)
        {
            statusCode = StatusCodes.Status500InternalServerError;
            message = "Internal server error.";
            LogTwitchFailure(logger, externalFailure.Kind, externalFailure);
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            message = "Internal server error.";
            LogUnhandledException(logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(message),
            cancellationToken: cancellationToken);

        return true;
    }

    [LoggerMessage(LogLevel.Information, "The client aborted the request.")]
    private static partial void LogRequestAborted(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Twitch rejected the access token or the application credentials. Check Twitch:ClientId and Twitch:ClientSecret.")]
    private static partial void LogTwitchAuthenticationFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(LogLevel.Error, "Twitch request failed ({Kind}).")]
    private static partial void LogTwitchFailure(
        ILogger logger,
        ExternalServiceErrorKind kind,
        Exception exception);

    [LoggerMessage(LogLevel.Error, "An unhandled exception occurred.")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception);
}
