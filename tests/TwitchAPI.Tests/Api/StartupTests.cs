using Microsoft.Extensions.Options;
using TwitchAPI.Tests.Support;

namespace TwitchAPI.Tests.Api;

public sealed class StartupTests
{
    [Theory]
    [InlineData("Twitch:ClientId")]
    [InlineData("Twitch:ClientSecret")]
    public void StartWhenTwitchCredentialIsEmptyReturnsValidationFailure(string setting)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { [setting] = "" });

        // Act
        var exception = Record.Exception(() => factory.CreateApiClient());

        // Assert
        AssertValidationFailureFor(setting, exception);
    }

    [Theory]
    [InlineData("Twitch:ClientId")]
    [InlineData("Twitch:ClientSecret")]
    public void StartWhenTwitchCredentialIsAbsentReturnsValidationFailure(string setting)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { [setting] = null });

        // Act
        var exception = Record.Exception(() => factory.CreateApiClient());

        // Assert
        AssertValidationFailureFor(setting, exception);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public void StartWhenTwitchTimeoutIsNotPositiveReturnsValidationFailure(string timeout)
    {
        // Arrange
        using var factory = new TwitchApiFactory(new Dictionary<string, string?> { ["Twitch:Timeout"] = timeout });

        // Act
        var exception = Record.Exception(() => factory.CreateApiClient());

        // Assert
        AssertValidationFailureFor("Twitch:Timeout", exception);
    }

    private static void AssertValidationFailureFor(string setting, Exception? exception)
    {
        var validation = Assert.IsType<OptionsValidationException>(exception);
        var member = setting.Split(':')[1];
        Assert.Contains(validation.Failures, failure => failure.Contains(member, StringComparison.Ordinal));
    }
}
