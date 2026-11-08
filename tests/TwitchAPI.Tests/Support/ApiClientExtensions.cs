using Microsoft.AspNetCore.Mvc.Testing;

namespace TwitchAPI.Tests.Support;

public static class ApiClientExtensions
{
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }
}
