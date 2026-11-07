# Twitch Analytics

A small API that reads public data from Twitch (user accounts and what is live right now) on behalf of its clients.

## Language

**Twitch User**:
Any Twitch account, identified by a numeric Twitch User ID, whether or not it ever broadcasts.
_Avoid_: Streamer, channel, account

**Twitch User ID**:
The numeric string Twitch assigns to a Twitch User; the only way clients look a user up.
_Avoid_: User name, login

**Live Stream**:
A broadcast that is on air on Twitch at the moment it is requested.
_Avoid_: Stream (unqualified), channel, broadcast

**App Access Token**:
The credential this API obtains from Twitch as an application (not on behalf of any Twitch User) to read public data.
_Avoid_: User token, OAuth token (unqualified), access token (unqualified)
