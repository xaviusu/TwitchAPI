using System.Diagnostics.CodeAnalysis;

namespace TwitchAPI.Domain;

public sealed record TwitchUserId
{
    private const int MaxLength = 20;

    private TwitchUserId(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? candidate, [NotNullWhen(true)] out TwitchUserId? twitchUserId)
    {
        twitchUserId = IsWellFormed(candidate) ? new TwitchUserId(candidate) : null;
        return twitchUserId is not null;
    }

    public override string ToString() => Value;

    private static bool IsWellFormed([NotNullWhen(true)] string? candidate) =>
        candidate is { Length: >= 1 and <= MaxLength } && candidate.All(char.IsAsciiDigit);
}
