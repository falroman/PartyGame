using FluentAssertions;
using NSubstitute;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Dictionary;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Services;
using Xunit;

namespace PartyGame.Tests.Unit;

public class DictionaryGameEngineTests
{
    private readonly IQuizQuestionBank _questionBank;
    private readonly IQuizGameEngine _sut;

    public DictionaryGameEngineTests()
    {
        _questionBank = Substitute.For<IQuizQuestionBank>();
        _sut = new QuizGameEngine(_questionBank);
    }

    private (Room Room, List<Guid> PlayerIds) CreateTestRoom(int playerCount)
    {
        var room = new Room
        {
            Code = "TEST",
            CreatedUtc = DateTime.UtcNow
        };
        var playerIds = new List<Guid>();

        for (int i = 0; i < playerCount; i++)
        {
            var playerId = Guid.NewGuid();
            playerIds.Add(playerId);
            room.Players[playerId] = new Player
            {
                PlayerId = playerId,
                DisplayName = $"Player{i + 1}",
                ConnectionId = $"conn{i}"
            };
        }

        return (room, playerIds);
    }

    private DictionaryQuestion CreateTestDictionaryQuestion(string word = "TestWord")
    {
        return new DictionaryQuestion
        {
            Word = word,
            Options = new List<string> { "Def A", "Def B", "Def C", "Def D" },
            CorrectIndex = 1,
            Definition = "Def B"
        };
    }

    private static List<RoundType> CreateTestRoundPlan()
    {
        return new List<RoundType>
        {
            RoundType.CategoryQuiz,
            RoundType.CategoryQuiz,
            RoundType.CategoryQuiz,
            RoundType.DictionaryGame
        };
    }

    #region StartDictionaryRound Tests

    [Fact]
    public void StartDictionaryRound_CreatesRoundWithCorrectType()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.QuestionNumber = 9; // All quiz questions done
        state.PlannedRoundIndex = 2; // At last CategoryQuiz round

        // Act
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);

        // Assert
        state.CurrentRound.Should().NotBeNull();
        state.CurrentRound!.Type.Should().Be(RoundType.DictionaryGame);
        state.DictionaryWordIndex.Should().Be(0);
    }

    [Fact]
    public void StartDictionaryRound_HasNoRoundLeader()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.QuestionNumber = 9;
        state.PlannedRoundIndex = 2;

        // Act
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);

        // Assert
        state.CurrentRound!.RoundLeaderPlayerId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void StartDictionaryRound_IncrementsRoundNumber()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.RoundNumber = 3;
        state.QuestionNumber = 9;
        state.PlannedRoundIndex = 2;

        // Act
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);

        // Assert
        state.RoundNumber.Should().Be(4);
    }

    #endregion

    #region StartDictionaryWord Tests

    [Fact]
    public void StartDictionaryWord_SetsPhaseToWordDisplay()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        var question = CreateTestDictionaryQuestion();

        // Act
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.DictionaryWord);
        state.DictionaryQuestion.Should().Be(question);
    }

    [Fact]
    public void StartDictionaryWord_IncrementsWordIndex()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        var question = CreateTestDictionaryQuestion();

        // Act
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);

        // Assert
        state.DictionaryWordIndex.Should().Be(1);
    }

    [Fact]
    public void StartDictionaryWord_TracksUsedWord()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        var question = CreateTestDictionaryQuestion("UniqueWord");

        // Act
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);

        // Assert
        state.UsedDictionaryWords.Should().Contain("UniqueWord");
    }

    [Fact]
    public void StartDictionaryWord_ClearsAnswers()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        
        // Simulate previous answers
        state.DictionaryAnswers[playerIds[0]] = 1;
        state.DictionaryAnswers[playerIds[1]] = 2;
        
        var question = CreateTestDictionaryQuestion();

        // Act
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);

        // Assert
        state.DictionaryAnswers.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }

    #endregion

    #region SubmitDictionaryAnswer Tests

    [Fact]
    public void SubmitDictionaryAnswer_RecordsAnswer()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        _sut.StartDictionaryWord(state, CreateTestDictionaryQuestion(), 3, DateTime.UtcNow);

        // Act
        _sut.SubmitDictionaryAnswer(state, playerIds[0], 2, DateTime.UtcNow);

        // Assert
        state.DictionaryAnswers[playerIds[0]].Should().Be(2);
    }

    [Fact]
    public void SubmitDictionaryAnswer_RecordsTimestamp()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        _sut.StartDictionaryWord(state, CreateTestDictionaryQuestion(), 3, DateTime.UtcNow);
        var answerTime = DateTime.UtcNow;

        // Act
        _sut.SubmitDictionaryAnswer(state, playerIds[0], 2, answerTime);

        // Assert
        state.DictionaryAnswerTimes[playerIds[0]].Should().Be(answerTime);
    }

    [Fact]
    public void SubmitDictionaryAnswer_IsIdempotent()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        _sut.StartDictionaryWord(state, CreateTestDictionaryQuestion(), 3, DateTime.UtcNow);

        // Act - Submit twice
        _sut.SubmitDictionaryAnswer(state, playerIds[0], 1, DateTime.UtcNow);
        _sut.SubmitDictionaryAnswer(state, playerIds[0], 3, DateTime.UtcNow.AddSeconds(1));

        // Assert - First answer wins
        state.DictionaryAnswers[playerIds[0]].Should().Be(1);
    }

    [Fact]
    public void SubmitDictionaryAnswer_IgnoresInvalidOptionIndex()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        _sut.StartDictionaryWord(state, CreateTestDictionaryQuestion(), 3, DateTime.UtcNow);

        // Act
        _sut.SubmitDictionaryAnswer(state, playerIds[0], 5, DateTime.UtcNow); // Invalid index

        // Assert
        state.DictionaryAnswers[playerIds[0]].Should().BeNull();
    }

    #endregion

    #region RevealDictionaryAnswer Tests

    [Fact]
    public void RevealDictionaryAnswer_AwardsCorrectPoints()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        
        var question = CreateTestDictionaryQuestion();
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);
        
        // Player 0 answers correctly
        _sut.SubmitDictionaryAnswer(state, playerIds[0], question.CorrectIndex, DateTime.UtcNow);
        // Player 1 answers incorrectly
        _sut.SubmitDictionaryAnswer(state, playerIds[1], (question.CorrectIndex + 1) % 4, DateTime.UtcNow);

        // Act
        _sut.RevealDictionaryAnswer(state, 5, DateTime.UtcNow);

        // Assert
        var player0Score = state.Scoreboard.First(p => p.PlayerId == playerIds[0]);
        var player1Score = state.Scoreboard.First(p => p.PlayerId == playerIds[1]);
        
        player0Score.Score.Should().BeGreaterThan(0);
        player1Score.Score.Should().Be(0);
    }

    [Fact]
    public void RevealDictionaryAnswer_AwardsSpeedBonus()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        
        var question = CreateTestDictionaryQuestion();
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);
        
        var baseTime = DateTime.UtcNow;
        // Player 0 answers first (fastest)
        _sut.SubmitDictionaryAnswer(state, playerIds[0], question.CorrectIndex, baseTime);
        // Player 1 answers second
        _sut.SubmitDictionaryAnswer(state, playerIds[1], question.CorrectIndex, baseTime.AddSeconds(2));

        // Act
        _sut.RevealDictionaryAnswer(state, 5, DateTime.UtcNow);

        // Assert
        var player0 = state.Scoreboard.First(p => p.PlayerId == playerIds[0]);
        var player1 = state.Scoreboard.First(p => p.PlayerId == playerIds[1]);
        
        player0.GotSpeedBonus.Should().BeTrue();
        player1.GotSpeedBonus.Should().BeFalse();
        player0.Score.Should().BeGreaterThan(player1.Score);
    }

    [Fact]
    public void RevealDictionaryAnswer_SetsPhaseToReveal()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        _sut.StartDictionaryWord(state, CreateTestDictionaryQuestion(), 3, DateTime.UtcNow);

        // Act
        _sut.RevealDictionaryAnswer(state, 5, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.Reveal);
    }

    #endregion

    #region HasMoreDictionaryWords Tests

    [Fact]
    public void HasMoreDictionaryWords_TrueWhenLessThanThree()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.DictionaryWordIndex = 1;

        // Act & Assert
        _sut.HasMoreDictionaryWords(state).Should().BeTrue();
    }

    [Fact]
    public void HasMoreDictionaryWords_FalseWhenThreeCompleted()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.DictionaryWordIndex = 3;

        // Act & Assert
        _sut.HasMoreDictionaryWords(state).Should().BeFalse();
    }

    #endregion

    #region ShouldStartDictionaryRound Tests

    [Fact]
    public void ShouldStartDictionaryRound_TrueWhenNextRoundIsDictionary()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.PlannedRoundIndex = 2; // At last CategoryQuiz, next is DictionaryGame

        // Act & Assert
        _sut.ShouldStartDictionaryRound(state).Should().BeTrue();
    }

    [Fact]
    public void ShouldStartDictionaryRound_FalseWhenNextRoundIsNotDictionary()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.PlannedRoundIndex = 0; // Next is CategoryQuiz

        // Act & Assert
        _sut.ShouldStartDictionaryRound(state).Should().BeFalse();
    }

    [Fact]
    public void ShouldStartDictionaryRound_FalseWhenAlreadyInDictionaryRound()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        state.PlannedRoundIndex = 2;
        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);

        // Act & Assert - Now at index 3 (DictionaryGame), next would be null
        _sut.ShouldStartDictionaryRound(state).Should().BeFalse();
    }

    #endregion

    #region IsValidDictionaryOption Tests

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    [InlineData(100, false)]
    public void IsValidDictionaryOption_ValidatesCorrectly(int optionIndex, bool expectedValid)
    {
        // Act & Assert
        _sut.IsValidDictionaryOption(optionIndex).Should().Be(expectedValid);
    }

    #endregion

    #region CatchUp Bonus Tests

    [Fact]
    public void RevealDictionaryAnswer_AwardsCatchUpBonusToBottomHalf()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4);
        var state = _sut.InitializeGame(room, "nl-BE", CreateTestRoundPlan());
        
        // Set up scores: Players 0,1 have high scores, players 2,3 have low scores
        state.Scoreboard.First(p => p.PlayerId == playerIds[0]).Score = 500;
        state.Scoreboard.First(p => p.PlayerId == playerIds[1]).Score = 400;
        state.Scoreboard.First(p => p.PlayerId == playerIds[2]).Score = 100;
        state.Scoreboard.First(p => p.PlayerId == playerIds[3]).Score = 0;

        _sut.StartDictionaryRound(state, 3, DateTime.UtcNow);
        
        var question = CreateTestDictionaryQuestion();
        _sut.StartDictionaryWord(state, question, 3, DateTime.UtcNow);
        
        // Both low-scoring players answer correctly
        var baseTime = DateTime.UtcNow;
        _sut.SubmitDictionaryAnswer(state, playerIds[2], question.CorrectIndex, baseTime.AddSeconds(1));
        _sut.SubmitDictionaryAnswer(state, playerIds[3], question.CorrectIndex, baseTime); // Fastest

        // Act
        _sut.RevealDictionaryAnswer(state, 5, DateTime.UtcNow);

        // Assert
        var player3 = state.Scoreboard.First(p => p.PlayerId == playerIds[3]);
        // Should get base (1000) + speed bonus (250) + catch-up (100) = 1350
        player3.PointsEarned.Should().Be(QuizGameState.DictionaryCorrectPoints + 
                                          QuizGameState.DictionarySpeedBonusPoints + 
                                          QuizGameState.DictionaryCatchUpBonusPoints);
    }

    #endregion
}
