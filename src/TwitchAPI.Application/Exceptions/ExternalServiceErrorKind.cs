namespace TwitchAPI.Application.Exceptions;

public enum ExternalServiceErrorKind
{
    Unavailable,
    Timeout,
    RateLimited,
    AuthenticationFailed
}
