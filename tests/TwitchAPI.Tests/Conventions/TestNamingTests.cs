using System.Reflection;
using System.Text.RegularExpressions;

namespace TwitchAPI.Tests.Conventions;

public sealed partial class TestNamingTests
{
    [Fact]
    public void TestMethodsWhenDiscoveredFollowMethodWhenConditionOutcome()
    {
        // Arrange
        var testMethods = typeof(TestNamingTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(FactAttribute), inherit: true))
            .ToList();

        // Act
        var offenders = testMethods
            .Where(method => !MethodWhenConditionOutcome().IsMatch(method.Name))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        // Assert
        Assert.NotEmpty(testMethods);
        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"^[A-Z]\w+When[A-Z]\w+$")]
    private static partial Regex MethodWhenConditionOutcome();
}
