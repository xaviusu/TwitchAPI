using System.Diagnostics.CodeAnalysis;

namespace TwitchAPI.Domain;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Live Stream is a domain term defined in GLOSSARY.md (a broadcast on air on Twitch), not a System.IO.Stream.")]
public sealed record LiveStream(string Title, string UserName);
