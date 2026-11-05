
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

public sealed class TwitchApiClient : ITwitchUserClient, ITwitchStreamClient
{
    public const int LiveStreamsPageSize = 20;

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

    public Task<TwitchUser?> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<TwitchUser?>(
            async () =>
            {
                TwitchUsersEnvelope? envelope =
                    await GetAsync<TwitchUsersEnvelope>(
                        $"users?id={Uri.EscapeDataString(id)}",
                        cancellationToken);

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
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<TwitchStreamInfo>> GetLiveStreamsAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<TwitchStreamInfo>>(
            async () =>
            {
                TwitchStreamsEnvelope? envelope =
                    await GetAsync<TwitchStreamsEnvelope>(
                        $"streams?first={LiveStreamsPageSize}",
                        cancellationToken);

                if (envelope?.Data is null)
                {
                    return [];
                }

                return envelope.Data
                    .Select(stream => new TwitchStreamInfo(
                        stream.Title,
                        stream.UserName))
                    .ToList();
            },
            cancellationToken);
    }

    private static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
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

    private async Task<T?> GetAsync<T>(
        string endpoint,
        CancellationToken cancellationToken)
    {
        string token = await GetTokenAsync(cancellationToken);

        using (var response = await SendRequestAsync(
            endpoint,
            token,
            cancellationToken))
        {
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return await ReadContentAsync<T>(
                    response,
                    cancellationToken);
            }
        }

        await _tokenProvider.InvalidateAsync(
            token,
            cancellationToken);

        string renewedToken = await GetTokenAsync(
            cancellationToken);

        using var retryResponse = await SendRequestAsync(
            endpoint,
            renewedToken,
            cancellationToken);

        return await ReadContentAsync<T>(
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
        string endpoint,
        string token,
        CancellationToken cancellationToken)
    {
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

    private static async Task<T?> ReadContentAsync<T>(
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

        return await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken);
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
