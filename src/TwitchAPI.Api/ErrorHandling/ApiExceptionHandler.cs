using System.Globalization;
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

        switch (exception)
        {
            case ExternalServiceException { Kind: ExternalServiceErrorKind.AuthenticationFailed } external:
                statusCode = StatusCodes.Status502BadGateway;
                message = "The upstream service is unavailable.";
                LogUpstreamAuthenticationFailure(logger, external);
                break;

            case ExternalServiceException { Kind: ExternalServiceErrorKind.Timeout } external:
                statusCode = StatusCodes.Status504GatewayTimeout;
                message = "The upstream service did not respond in time.";
                LogUpstreamFailure(logger, external.Kind, external);
                break;

            case ExternalServiceException { Kind: ExternalServiceErrorKind.RateLimited } external:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                message = "The upstream service is busy. Try again later.";
                SetRetryAfter(httpContext, external.RetryAfter);
                LogUpstreamFailure(logger, external.Kind, external);
                break;

            case ExternalServiceException external:
                statusCode = StatusCodes.Status502BadGateway;
                message = "The upstream service is unavailable.";
                LogUpstreamFailure(logger, external.Kind, external);
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                message = "Internal server error.";
                LogUnhandledException(logger, exception);
                break;
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(message),
            cancellationToken: cancellationToken);

        return true;
    }

    private static void SetRetryAfter(
        HttpContext httpContext,
        TimeSpan? retryAfter)
    {
        if (retryAfter is not { } delay)
        {
            return;
        }

        long seconds = Math.Max(1, (long)Math.Ceiling(delay.TotalSeconds));

        httpContext.Response.Headers.RetryAfter =
            seconds.ToString(CultureInfo.InvariantCulture);
    }

    [LoggerMessage(LogLevel.Information, "The client aborted the request.")]
    private static partial void LogRequestAborted(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Twitch rejected the application credentials. Check Twitch:ClientId and Twitch:ClientSecret.")]
    private static partial void LogUpstreamAuthenticationFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(LogLevel.Warning, "Twitch request failed ({Kind}).")]
    private static partial void LogUpstreamFailure(
        ILogger logger,
        ExternalServiceErrorKind kind,
        Exception exception);

    [LoggerMessage(LogLevel.Error, "An unhandled exception occurred.")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception);
}
