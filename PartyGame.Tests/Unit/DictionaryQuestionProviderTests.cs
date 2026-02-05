using FluentAssertions;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Dictionary;
using PartyGame.Server.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace PartyGame.Tests.Unit;

public class DictionaryQuestionProviderTests
{
    private readonly IDictionaryQuestionProvider _sut;
    private readonly ILogger<DictionaryQuestionProvider> _logger;

    public DictionaryQuestionProviderTests()
    {
        _logger = Substitute.For<ILogger<DictionaryQuestionProvider>>();
        _sut = new DictionaryQuestionProvider(_logger);
    }

    [Fact]
    public void GetRandomQuestion_ReturnsQuestionWithFourOptions()
    {
        // Act
        var question = _sut.GetRandomQuestion("nl-BE");

        // Assert
        question.Should().NotBeNull();
        question!.Options.Should().HaveCount(4);
        question.Word.Should().NotBeNullOrEmpty();
        question.Definition.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetRandomQuestion_CorrectIndexIsValid()
    {
        // Act
        var question = _sut.GetRandomQuestion("nl-BE");

        // Assert
        question.Should().NotBeNull();
        question!.CorrectIndex.Should().BeInRange(0, 3);
    }

    [Fact]
    public void GetRandomQuestion_CorrectDefinitionIsInOptions()
    {
        // Act
        var question = _sut.GetRandomQuestion("nl-BE");

        // Assert
        question.Should().NotBeNull();
        question!.Options[question.CorrectIndex].Should().Be(question.Definition);
    }

    [Fact]
    public void GetRandomQuestion_DistractorsAreDifferentFromCorrect()
    {
        // Act
        var question = _sut.GetRandomQuestion("nl-BE");

        // Assert
        question.Should().NotBeNull();
        
        for (int i = 0; i < question!.Options.Count; i++)
        {
            if (i != question.CorrectIndex)
            {
                question.Options[i].Should().NotBe(question.Definition,
                    because: "distractor should not equal correct definition");
            }
        }
    }

    [Fact]
    public void GetRandomQuestion_ExcludesUsedWords()
    {
        // Arrange
        var usedWords = new HashSet<string>();
        
        // Get first question
        var question1 = _sut.GetRandomQuestion("nl-BE", usedWords);
        question1.Should().NotBeNull();
        usedWords.Add(question1!.Word);

        // Act - Get second question, excluding first word
        var question2 = _sut.GetRandomQuestion("nl-BE", usedWords);

        // Assert
        question2.Should().NotBeNull();
        question2!.Word.Should().NotBe(question1.Word,
            because: "second question should have different word");
    }

    [Fact]
    public void GetRandomQuestion_ReturnsNullForUnknownLocale()
    {
        // Act
        var question = _sut.GetRandomQuestion("unknown-locale");

        // Assert
        question.Should().BeNull();
    }

    [Fact]
    public void GetWordCount_ReturnsCorrectCount()
    {
        // Act
        var count = _sut.GetWordCount("nl-BE");

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetWordCount_ReturnsZeroForUnknownLocale()
    {
        // Act
        var count = _sut.GetWordCount("unknown-locale");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetRandomQuestion_AllOptionsAreUnique()
    {
        // Act
        var question = _sut.GetRandomQuestion("nl-BE");

        // Assert
        question.Should().NotBeNull();
        question!.Options.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetRandomQuestion_MultipleCallsReturnDifferentWords()
    {
        // Arrange
        var words = new HashSet<string>();

        // Act - Get 5 questions
        for (int i = 0; i < 5; i++)
        {
            var question = _sut.GetRandomQuestion("nl-BE", words);
            if (question != null)
            {
                words.Add(question.Word);
            }
        }

        // Assert - Should have 5 unique words
        words.Should().HaveCount(5);
    }
}
