using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace TwitchAPI.Tests.Support;

public sealed class TwitchApiFactory : WebApplicationFactory<Program>
{
    public const string ClientId = "test-client-id";
    public const string ClientSecret = "test-client-secret";

    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Twitch:ClientId"] = ClientId,
        ["Twitch:ClientSecret"] = ClientSecret,
    };

    public TwitchApiFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            _settings[key] = value;
        }
    }

    public FakeTwitch Twitch { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.ConfigureHttpClientDefaults(httpClient =>
                httpClient.ConfigurePrimaryHttpMessageHandler(Twitch.CreateHandler));
        });
    }
}
