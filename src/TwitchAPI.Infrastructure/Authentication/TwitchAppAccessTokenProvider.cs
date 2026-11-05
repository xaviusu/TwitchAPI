
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TwitchAPI.Infrastructure.Authentication.Models;

namespace TwitchAPI.Infrastructure.Authentication;

public sealed class TwitchAppAccessTokenProvider
    : ITwitchAppAccessTokenProvider, IDisposable
{
    public const string HttpClientName = "TwitchOAuth";

    private static readonly TimeSpan MaxExpiryMargin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwitchOAuthOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public TwitchAppAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<TwitchOAuthOptions> options,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_cachedToken is not null &&
                _timeProvider.GetUtcNow() < _expiresAt)
            {
                return _cachedToken;
            }

            var client = _httpClientFactory.CreateClient(
                HttpClientName);

            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["grant_type"] = "client_credentials"
                });

            using var response = await client.PostAsync(
                "token",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            TwitchOAuthTokenResponse? tokenResponse;

            try
            {
                tokenResponse = await response.Content
                    .ReadFromJsonAsync<TwitchOAuthTokenResponse>(
                        cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "Twitch returned an invalid OAuth response.",
                    exception);
            }

            string? token = tokenResponse?.AccessToken;
            long? expiresIn = tokenResponse?.ExpiresIn;

            if (string.IsNullOrWhiteSpace(token) || expiresIn is null or <= 0)
            {
                throw new InvalidOperationException(
                    "Twitch returned an invalid OAuth response.");
            }

            DateTimeOffset expiresAt;

            try
            {
                TimeSpan lifetime = TimeSpan.FromSeconds(expiresIn.Value);
                TimeSpan margin = lifetime / 10 < MaxExpiryMargin
                    ? lifetime / 10
                    : MaxExpiryMargin;

                expiresAt = _timeProvider.GetUtcNow().Add(lifetime - margin);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidOperationException(
                    "Twitch returned an invalid OAuth response.",
                    exception);
            }

            _cachedToken = token;
            _expiresAt = expiresAt;

            return token;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task InvalidateAsync(
        string rejectedToken,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_cachedToken == rejectedToken)
            {
                _cachedToken = null;
                _expiresAt = default;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
