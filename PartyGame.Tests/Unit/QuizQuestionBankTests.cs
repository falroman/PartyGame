using Microsoft.Extensions.Logging;
using NSubstitute;
using PartyGame.Core.Exceptions;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Services;
using System.Text.Json;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for JsonQuizQuestionBank.
/// </summary>
public class QuizQuestionBankTests : IDisposable
{
    private readonly string _testContentPath;
    private readonly ILogger<JsonQuizQuestionBank> _logger;

    public QuizQuestionBankTests()
    {
        _testContentPath = Path.Combine(Path.GetTempPath(), $"QuizTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_testContentPath, "Content"));
        _logger = Substitute.For<ILogger<JsonQuizQuestionBank>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testContentPath))
        {
            Directory.Delete(_testContentPath, recursive: true);
        }
    }

    private void WriteTestPack(string fileName, object content)
    {
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var filePath = Path.Combine(_testContentPath, "Content", fileName);
        File.WriteAllText(filePath, json);
    }

    private QuestionPack CreateValidPack(string packId = "test-pack", string locale = "nl-BE", int questionCount = 10)
    {
        var pack = new QuestionPack
        {
            SchemaVersion = 1,
            PackId = packId,
            Title = "Test Pack",
            Locale = locale,
            Tags = new List<string> { "test" },
            Questions = new List<QuizQuestion>()
        };

        for (int i = 1; i <= questionCount; i++)
        {
            pack.Questions.Add(new QuizQuestion
            {
                Id = $"q-{i:D3}",
                Category = i % 2 == 0 ? "Category A" : "Category B",
                Difficulty = (i % 5) + 1,
                Question = $"Test question {i}?",
                Options = new List<QuizOption>
                {
                    new() { Key = "A", Text = "Option A" },
                    new() { Key = "B", Text = "Option B" },
                    new() { Key = "C", Text = "Option C" },
                    new() { Key = "D", Text = "Option D" }
                },
                CorrectOptionKey = "A",
                Explanation = $"Explanation for question {i}",
                ShuffleOptions = true,
                Tags = new List<string> { "tag1", i % 2 == 0 ? "even" : "odd" },
                Source = new SourceInfo { Type = "original" }
            });
        }

        return pack;
    }

    #region Loading Tests

    [Fact]
    public void LoadValidPack_LoadsAllQuestions()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 15);
        WriteTestPack("questions.nl-BE.json", pack);

        // Act
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Assert
        bank.GetCount("nl-BE").Should().Be(15);
        bank.GetAvailableLocales().Should().Contain("nl-be");
    }

    [Fact]
    public void LoadValidPack_LoadsAtLeast10Questions()
    {
        // Arrange - Use the actual content from the server
        var serverContentPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PartyGame.Server"));
        
        // Skip if running outside the solution context
        if (!Directory.Exists(Path.Combine(serverContentPath, "Content")))
        {
            return; // Skip this test if Content folder doesn't exist
        }

        // Act
        var bank = new JsonQuizQuestionBank(serverContentPath, _logger);

        // Assert
        var nlBeCount = bank.GetCount("nl-BE");
        nlBeCount.Should().BeGreaterOrEqualTo(10, 
            "The question pack should contain at least 10 questions");
    }

    [Fact]
    public void LoadEmptyContentFolder_LoadsWithoutError()
    {
        // Arrange - Empty content folder (no files)
        
        // Act
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Assert
        bank.GetCount("nl-BE").Should().Be(0);
        bank.GetAvailableLocales().Should().BeEmpty();
    }

    [Fact]
    public void LoadInvalidJson_ThrowsWithClearMessage()
    {
        // Arrange
        var filePath = Path.Combine(_testContentPath, "Content", "questions.test.json");
        File.WriteAllText(filePath, "{ invalid json }");

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .WithMessage("*Invalid JSON*");
    }

    [Fact]
    public void LoadPackWithEmptyPackId_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack();
        pack.PackId = "";
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("PackId")));
    }

    [Fact]
    public void LoadPackWithDuplicateQuestionIds_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 2);
        pack.Questions[0].Id = "duplicate-id";
        pack.Questions[1].Id = "duplicate-id";
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("Duplicate")));
    }

    [Fact]
    public void LoadPackWithInvalidDifficulty_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 1);
        pack.Questions[0].Difficulty = 6; // Invalid: must be 1-5
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("Difficulty")));
    }

    [Fact]
    public void LoadPackWithWrongOptionCount_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 1);
        pack.Questions[0].Options = new List<QuizOption>
        {
            new() { Key = "A", Text = "Only one option" }
        };
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("4 options")));
    }

    [Fact]
    public void LoadPackWithInvalidCorrectOptionKey_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 1);
        pack.Questions[0].CorrectOptionKey = "Z"; // Not in options
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("CorrectOptionKey")));
    }

    [Fact]
    public void LoadPackWithEmptyQuestionText_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 1);
        pack.Questions[0].Question = "";
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("Question text")));
    }

    [Fact]
    public void LoadPackWithDuplicateOptionKeys_ThrowsValidationError()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 1);
        pack.Questions[0].Options = new List<QuizOption>
        {
            new() { Key = "A", Text = "Option 1" },
            new() { Key = "A", Text = "Option 2 with same key" },
            new() { Key = "C", Text = "Option 3" },
            new() { Key = "D", Text = "Option 4" }
        };
        WriteTestPack("questions.test.json", pack);

        // Act & Assert
        var act = () => new JsonQuizQuestionBank(_testContentPath, _logger);
        act.Should().Throw<QuestionPackValidationException>()
            .Where(e => e.ValidationErrors.Any(err => err.Contains("Duplicate option key")));
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsAllQuestionsForLocale()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 5);
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var questions = bank.GetAll("nl-BE").ToList();

        // Assert
        questions.Should().HaveCount(5);
    }

    [Fact]
    public void GetAll_IsCaseInsensitive()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var questionsLower = bank.GetAll("nl-be").ToList();
        var questionsUpper = bank.GetAll("NL-BE").ToList();
        var questionsMixed = bank.GetAll("nL-Be").ToList();

        // Assert
        questionsLower.Should().HaveCount(10);
        questionsUpper.Should().HaveCount(10);
        questionsMixed.Should().HaveCount(10);
    }

    [Fact]
    public void GetAll_ReturnsEmptyForUnknownLocale()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var questions = bank.GetAll("fr-FR").ToList();

        // Assert
        questions.Should().BeEmpty();
    }

    #endregion

    #region GetRandom Tests

    [Fact]
    public void GetRandom_ReturnsQuestionForValidLocale()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var question = bank.GetRandom("nl-BE");

        // Assert
        question.Should().NotBeNull();
        question!.Locale.Should().Be("nl-BE");
    }

    [Fact]
    public void GetRandom_ReturnsNullForUnknownLocale()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var question = bank.GetRandom("fr-FR");

        // Assert
        question.Should().BeNull();
    }

    [Fact]
    public void GetRandom_FiltersByCategory()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act - Get 10 random questions with category filter
        var questions = Enumerable.Range(0, 10)
            .Select(_ => bank.GetRandom("nl-BE", category: "Category A"))
            .Where(q => q != null)
            .ToList();

        // Assert
        questions.Should().NotBeEmpty();
        questions.Should().AllSatisfy(q => q!.Category.Should().Be("Category A"));
    }

    [Fact]
    public void GetRandom_FiltersByDifficulty()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var questions = Enumerable.Range(0, 10)
            .Select(_ => bank.GetRandom("nl-BE", difficulty: 3))
            .Where(q => q != null)
            .ToList();

        // Assert
        questions.Should().NotBeEmpty();
        questions.Should().AllSatisfy(q => q!.Difficulty.Should().Be(3));
    }

    [Fact]
    public void GetRandom_FiltersByTags()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var questions = Enumerable.Range(0, 10)
            .Select(_ => bank.GetRandom("nl-BE", tags: new[] { "even" }))
            .Where(q => q != null)
            .ToList();

        // Assert
        questions.Should().NotBeEmpty();
        questions.Should().AllSatisfy(q => q!.Tags.Should().Contain("even"));
    }

    [Fact]
    public void GetRandom_ExcludesSpecifiedIds()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 3);
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        var excludeIds = new[] { "q-001", "q-002" };

        // Act
        var questions = Enumerable.Range(0, 10)
            .Select(_ => bank.GetRandom("nl-BE", excludeIds: excludeIds))
            .Where(q => q != null)
            .ToList();

        // Assert
        questions.Should().NotBeEmpty();
        questions.Should().AllSatisfy(q => q!.Id.Should().NotBe("q-001").And.NotBe("q-002"));
    }

    [Fact]
    public void GetRandom_ReturnsNullWhenAllExcluded()
    {
        // Arrange
        var pack = CreateValidPack(questionCount: 2);
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        var excludeIds = new[] { "q-001", "q-002" };

        // Act
        var question = bank.GetRandom("nl-BE", excludeIds: excludeIds);

        // Assert
        question.Should().BeNull();
    }

    #endregion

    #region TryGetById Tests

    [Fact]
    public void TryGetById_ReturnsTrueForExistingQuestion()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var found = bank.TryGetById("q-005", out var question);

        // Assert
        found.Should().BeTrue();
        question.Should().NotBeNull();
        question!.Id.Should().Be("q-005");
    }

    [Fact]
    public void TryGetById_ReturnsFalseForUnknownId()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var found = bank.TryGetById("nonexistent", out var question);

        // Assert
        found.Should().BeFalse();
        question.Should().BeNull();
    }

    #endregion

    #region GetAvailableCategories Tests

    [Fact]
    public void GetAvailableCategories_ReturnsAllCategories()
    {
        // Arrange
        var pack = CreateValidPack();
        WriteTestPack("questions.nl-BE.json", pack);
        var bank = new JsonQuizQuestionBank(_testContentPath, _logger);

        // Act
        var categories = bank.GetAvailableCategories("nl-BE");

        // Assert
        categories.Should().Contain("Category A");
        categories.Should().Contain("Category B");
    }

    #endregion
}
