namespace TwitchAPI.Api.ErrorHandling;

internal static class EmptyBodyExceptionHandlerExtensions
{
    public static IApplicationBuilder UseEmptyBodyExceptionHandler(this IApplicationBuilder app) =>
        app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(_ => Task.CompletedTask));
}
