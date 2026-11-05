using TwitchAPI.Api.Validation;

namespace TwitchAPI.Tests.Api;

public sealed class TwitchUserIdValidatorTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("123456789")]
    [InlineData("000123")]
    [InlineData("12345678901234567890")]
    public void IsValidWithNumericIdReturnsTrue(string id)
    {
        // Act
        bool result = TwitchUserIdValidator.IsValid(id);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("123abc")]
    [InlineData("-123")]
    [InlineData("12.34")]
    [InlineData("１２３")]
    public void IsValidWithInvalidIdReturnsFalse(string? id)
    {
        // Act
        bool result = TwitchUserIdValidator.IsValid(id);

        // Assert
        Assert.False(result);
    }
}
