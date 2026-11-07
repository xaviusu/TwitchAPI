using System.Diagnostics.CodeAnalysis;

namespace TwitchAPI.Domain;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Live Stream is a glossary term for a Twitch broadcast, not a System.IO.Stream.")]
public sealed record LiveStream(string Title, string UserName);
