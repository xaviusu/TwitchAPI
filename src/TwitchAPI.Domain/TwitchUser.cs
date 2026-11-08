namespace TwitchAPI.Domain;

public sealed record TwitchUser(
    string Id,
    string Login,
    string DisplayName,
    string Type,
    string BroadcasterType,
    string Description,
    string ProfileImageUrl,
    string OfflineImageUrl,
    long ViewCount,
    string CreatedAt);
