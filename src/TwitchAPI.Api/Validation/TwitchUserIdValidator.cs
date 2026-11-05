namespace TwitchAPI.Api.Validation;

public static class TwitchUserIdValidator
{

    public static bool IsValid(string? id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && id.All(char.IsAsciiDigit);
    }
}
