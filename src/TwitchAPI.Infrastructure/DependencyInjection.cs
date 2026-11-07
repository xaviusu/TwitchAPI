using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TwitchAPI.Application.Twitch;
using TwitchAPI.Infrastructure.Twitch;

namespace TwitchAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTwitchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TwitchOptions>()
            .Bind(configuration.GetSection(TwitchOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.Timeout > TimeSpan.Zero, "Twitch:Timeout must be greater than zero.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient<IAppAccessTokenProvider, AppAccessTokenProvider>((provider, httpClient) =>
        {
            var options = provider.GetRequiredService<IOptions<TwitchOptions>>().Value;
            httpClient.BaseAddress = options.AuthBaseAddress;
            httpClient.Timeout = options.Timeout;
        });
        services.AddHttpClient<ITwitchClient, HelixTwitchClient>((provider, httpClient) =>
        {
            var options = provider.GetRequiredService<IOptions<TwitchOptions>>().Value;
            httpClient.BaseAddress = options.HelixBaseAddress;
            httpClient.Timeout = options.Timeout;
        });

        return services;
    }
}
