namespace PartyGame.Tests.Unit;

/// <summary>
/// Sanity tests to verify the test infrastructure is working correctly.
/// </summary>
public class SanityTests
{
    [Fact]
    public void TrueIsTrue_SanityCheck()
    {
        // Arrange & Act & Assert
        true.Should().BeTrue();
    }

    [Fact]
    public void FluentAssertions_WorksCorrectly()
    {
        // Arrange
        var number = 42;

        // Act & Assert
        number.Should().Be(42);
        number.Should().BePositive();
        number.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("hello", 5)]
    [InlineData("world", 5)]
    [InlineData("", 0)]
    public void StringLength_ReturnsExpectedLength(string input, int expectedLength)
    {
        // Act & Assert
        input.Length.Should().Be(expectedLength);
    }
}
