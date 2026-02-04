using PartyGame.Core.Services;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for RoomCodeGenerator.
/// </summary>
public class RoomCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsCodeOfCorrectLength()
    {
        // Arrange
        var generator = new RoomCodeGenerator();
        var existingCodes = new HashSet<string>();

        // Act
        var code = generator.Generate(existingCodes);

        // Assert
        code.Should().HaveLength(4);
    }

    [Fact]
    public void Generate_ReturnsUppercaseAlphanumericCode()
    {
        // Arrange
        var generator = new RoomCodeGenerator();
        var existingCodes = new HashSet<string>();

        // Act
        var code = generator.Generate(existingCodes);

        // Assert
        code.Should().MatchRegex("^[A-Z0-9]{4}$");
    }

    [Fact]
    public void Generate_ExcludesAmbiguousCharacters()
    {
        // Arrange
        var generator = new RoomCodeGenerator();
        var existingCodes = new HashSet<string>();
        var ambiguousChars = new[] { '0', 'O', 'I', '1', 'L' };

        // Act - Generate many codes to increase confidence
        var codes = Enumerable.Range(0, 100)
            .Select(_ => generator.Generate(existingCodes))
            .ToList();

        // Assert
        foreach (var code in codes)
        {
            code.Should().NotContainAny(ambiguousChars.Select(c => c.ToString()).ToArray());
        }
    }

    [Fact]
    public void Generate_AvoidsExistingCodes()
    {
        // Arrange
        var generator = new RoomCodeGenerator(seed: 42); // Fixed seed for reproducibility
        var existingCodes = new HashSet<string>();
        
        // Generate first code
        var firstCode = generator.Generate(existingCodes);
        existingCodes.Add(firstCode);

        // Act - Generate second code
        var secondCode = generator.Generate(existingCodes);

        // Assert
        secondCode.Should().NotBe(firstCode);
    }

    [Fact]
    public void Generate_WithSameSeed_ProducesDeterministicFirstCode()
    {
        // Arrange
        var generator1 = new RoomCodeGenerator(seed: 12345);
        var generator2 = new RoomCodeGenerator(seed: 12345);
        var existingCodes = new HashSet<string>();

        // Act
        var code1 = generator1.Generate(existingCodes);
        var code2 = generator2.Generate(existingCodes);

        // Assert
        code1.Should().Be(code2);
    }

    [Fact]
    public void Generate_ProducesUniqueCodesOverMultipleGenerations()
    {
        // Arrange
        var generator = new RoomCodeGenerator();
        var generatedCodes = new HashSet<string>();
        const int numberOfCodes = 50;

        // Act
        for (int i = 0; i < numberOfCodes; i++)
        {
            var code = generator.Generate(generatedCodes);
            generatedCodes.Add(code);
        }

        // Assert
        generatedCodes.Should().HaveCount(numberOfCodes);
    }
}
