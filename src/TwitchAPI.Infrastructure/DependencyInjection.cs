
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwitchAPI.Application.Interfaces;
using TwitchAPI.Infrastructure.Authentication;
using TwitchAPI.Infrastructure.Clients;

namespace TwitchAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TwitchOAuthOptions>()
            .Bind(configuration.GetSection(
                TwitchOAuthOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ClientSecret),
                "Twitch OAuth credentials are not configured.")
            .ValidateOnStart();

        services.AddHttpClient(
            TwitchAppAccessTokenProvider.HttpClientName,
            client =>
            {
                client.BaseAddress = new Uri(
                    "https://id.twitch.tv/oauth2/");

                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<
            ITwitchAppAccessTokenProvider,
            TwitchAppAccessTokenProvider>();

        services.AddHttpClient<TwitchApiClient>(
            client =>
            {
                client.BaseAddress = new Uri(
                    "https://api.twitch.tv/helix/");

                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services.AddTransient<ITwitchUserClient>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TwitchApiClient>());

        services.AddTransient<ITwitchStreamClient>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TwitchApiClient>());

        return services;
    }
}
