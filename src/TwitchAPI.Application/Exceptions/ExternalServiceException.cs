namespace TwitchAPI.Application.Exceptions;

public sealed class ExternalServiceException : Exception
{
    public ExternalServiceException()
        : this(ExternalServiceErrorKind.Unavailable, "The external service is unavailable.")
    {
    }

    public ExternalServiceException(string message)
        : this(ExternalServiceErrorKind.Unavailable, message)
    {
    }

    public ExternalServiceException(string message, Exception innerException)
        : this(ExternalServiceErrorKind.Unavailable, message, innerException)
    {
    }

    public ExternalServiceException(
        ExternalServiceErrorKind kind,
        string message,
        Exception? innerException = null,
        TimeSpan? retryAfter = null)
        : base(message, innerException)
    {
        Kind = kind;
        RetryAfter = retryAfter;
    }

    public ExternalServiceErrorKind Kind { get; }

    public TimeSpan? RetryAfter { get; }
}
