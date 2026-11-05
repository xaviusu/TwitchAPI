
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Exceptions;
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Application.Models;
using TwitchAPI.Infrastructure.Authentication;
using TwitchAPI.Infrastructure.Clients.Models;

namespace TwitchAPI.Infrastructure.Clients;

public sealed class TwitchApiClient : ITwitchUserClient
{
    private readonly HttpClient _httpClient;
    private readonly ITwitchAppAccessTokenProvider _tokenProvider;
    private readonly TwitchOAuthOptions _options;

    public TwitchApiClient(
        HttpClient httpClient,
        ITwitchAppAccessTokenProvider tokenProvider,
        IOptions<TwitchOAuthOptions> options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    public async Task<TwitchUser?> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetUserCoreAsync(
                id,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.Timeout,
                "Twitch did not respond in time.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.Unavailable,
                "The request to Twitch failed.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.Unavailable,
                "Twitch returned an invalid response.",
                exception);
        }
    }

    private async Task<TwitchUser?> GetUserCoreAsync(
        string id,
        CancellationToken cancellationToken)
    {
        string token = await GetTokenAsync(cancellationToken);

        using var response = await SendRequestAsync(
            id,
            token,
            cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await ReadUserAsync(
                response,
                cancellationToken);
        }

        await _tokenProvider.InvalidateAsync(
            token,
            cancellationToken);

        string renewedToken = await GetTokenAsync(
            cancellationToken);

        using var retryResponse = await SendRequestAsync(
            id,
            renewedToken,
            cancellationToken);

        return await ReadUserAsync(
            retryResponse,
            cancellationToken);
    }

    private async Task<string> GetTokenAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _tokenProvider.GetAccessTokenAsync(
                cancellationToken);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is
                HttpStatusCode.BadRequest or
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.AuthenticationFailed,
                "Twitch rejected the application credentials.",
                exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.Unavailable,
                "Could not obtain a Twitch access token.",
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string id,
        string token,
        CancellationToken cancellationToken)
    {
        string endpoint =
            $"users?id={Uri.EscapeDataString(id)}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            endpoint);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Headers.Add(
            "Client-Id",
            _options.ClientId);

        return await _httpClient.SendAsync(
            request,
            cancellationToken);
    }

    private static async Task<TwitchUser?> ReadUserAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.AuthenticationFailed,
                "Twitch rejected a freshly issued access token.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ExternalServiceException(
                ExternalServiceErrorKind.RateLimited,
                "Twitch rate limit reached.",
                retryAfter: GetRetryAfter(response));
        }

        response.EnsureSuccessStatusCode();

        TwitchUsersEnvelope? envelope =
            await response.Content
                .ReadFromJsonAsync<TwitchUsersEnvelope>(
                    cancellationToken: cancellationToken);

        TwitchUserPayload? user = envelope?.Data?.FirstOrDefault();

        if (user is null)
        {
            return null;
        }

        return new TwitchUser(
            user.Id,
            user.Login,
            user.DisplayName,
            user.Type,
            user.BroadcasterType,
            user.Description,
            user.ProfileImageUrl,
            user.OfflineImageUrl,
            user.ViewCount,
            user.CreatedAt);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan untilDate = date - DateTimeOffset.UtcNow;
            return untilDate > TimeSpan.Zero ? untilDate : TimeSpan.Zero;
        }

        if (response.Headers.TryGetValues("Ratelimit-Reset", out IEnumerable<string>? values) &&
            long.TryParse(
                values.FirstOrDefault(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long resetUnixSeconds) &&
            resetUnixSeconds is > 0 and < 253_402_300_799)
        {
            TimeSpan untilReset =
                DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds) - DateTimeOffset.UtcNow;

            return untilReset > TimeSpan.Zero ? untilReset : TimeSpan.Zero;
        }

        return null;
    }
}
