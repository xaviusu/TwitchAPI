namespace TwitchAPI.Api.ErrorHandling;

internal static class RegisteredExceptionHandlerExtensions
{
    public static IApplicationBuilder UseRegisteredExceptionHandlers(this IApplicationBuilder app) =>
        app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(_ => Task.CompletedTask));
}
