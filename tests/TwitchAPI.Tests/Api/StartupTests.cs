using Microsoft.Extensions.Options;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class StartupTests
{
    [Theory]
    [InlineData("Twitch:ClientId")]
    [InlineData("Twitch:ClientSecret")]
    public void StartWhenTwitchCredentialIsMissingReturnsValidationFailure(string missingSetting)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { [missingSetting] = "" });

        // Act
        var exception = Record.Exception(() => factory.CreateApiClient());

        // Assert
        var validation = Assert.IsType<OptionsValidationException>(exception);
        Assert.Contains(validation.Failures, failure => failure.Contains(missingSetting.Split(':')[1], StringComparison.Ordinal));
    }
}
