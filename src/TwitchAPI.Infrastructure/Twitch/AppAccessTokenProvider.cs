using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Twitch;

namespace TwitchAPI.Infrastructure.Twitch;

internal sealed class AppAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<TwitchOptions> options,
    TimeProvider timeProvider) : IAppAccessTokenProvider, IDisposable
{
    public const string HttpClientName = "TwitchAuth";

    private static readonly Uri TokenUri = new("oauth2/token", UriKind.Relative);
    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _renewalLock = new(1, 1);
    private volatile CachedToken? _cachedToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetValidToken(out var accessToken))
        {
            return accessToken;
        }

        await _renewalLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetValidToken(out accessToken))
            {
                return accessToken;
            }

            var renewedToken = await RequestAccessTokenAsync(cancellationToken);
            _cachedToken = renewedToken;
            return renewedToken.AccessToken;
        }
        finally
        {
            _renewalLock.Release();
        }
    }

    public void Invalidate(string rejectedAccessToken)
    {
        var cachedToken = _cachedToken;
        if (cachedToken?.AccessToken == rejectedAccessToken)
        {
            Interlocked.CompareExchange(ref _cachedToken, null, cachedToken);
        }
    }

    public void Dispose() => _renewalLock.Dispose();

    private bool TryGetValidToken([NotNullWhen(true)] out string? accessToken)
    {
        var cachedToken = _cachedToken;
        accessToken = cachedToken is not null && timeProvider.GetUtcNow() < cachedToken.RenewAt
            ? cachedToken.AccessToken
            : null;
        return accessToken is not null;
    }

    private async Task<CachedToken> RequestAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.Value.ClientId,
            ["client_secret"] = options.Value.ClientSecret,
            ["grant_type"] = "client_credentials",
        });
        var requestedAt = timeProvider.GetUtcNow();
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsync(TokenUri, content, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new TwitchUnauthorizedException(
                $"Twitch rejected the client credentials with status {(int)response.StatusCode}.");
        }

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TwitchJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("Twitch returned an empty token response.");
        return new CachedToken(
            AccessToken: token.AccessToken,
            RenewAt: requestedAt + TimeSpan.FromSeconds(token.ExpiresIn) - ExpirySafetyMargin);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset RenewAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);
}
