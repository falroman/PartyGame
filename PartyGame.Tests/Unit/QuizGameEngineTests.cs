using NSubstitute;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Services;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for QuizGameEngine.
/// </summary>
public class QuizGameEngineTests
{
    private readonly IQuizQuestionBank _questionBank;
    private readonly QuizGameEngine _sut;

    public QuizGameEngineTests()
    {
        _questionBank = Substitute.For<IQuizQuestionBank>();
        _sut = new QuizGameEngine(_questionBank);
    }

    private (Room room, List<Guid> playerIds) CreateTestRoom(int playerCount = 2)
    {
        var room = new Room
        {
            Code = "TEST",
            Status = RoomStatus.InGame,
            Players = new Dictionary<Guid, Player>()
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
                IsConnected = true,
                Score = 0
            };
        }

        return (room, playerIds);
    }

    private QuizQuestion CreateTestQuestion(string id = "q1")
    {
        return new QuizQuestion
        {
            Id = id,
            Question = "What is 2 + 2?",
            Options = new List<QuizOption>
            {
                new() { Key = "A", Text = "3" },
                new() { Key = "B", Text = "4" },
                new() { Key = "C", Text = "5" },
                new() { Key = "D", Text = "6" }
            },
            CorrectOptionKey = "B",
            Explanation = "2 + 2 = 4",
            Difficulty = 1,
            Category = "Math"
        };
    }

    private void SetupQuestionBankMock(QuizQuestion question)
    {
        _questionBank.GetRandom(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<IEnumerable<string>?>())
            .Returns(question);
    }

    #region InitializeGame Tests

    [Fact]
    public void InitializeGame_CreatesStateWithAllPlayers()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);

        // Act
        var state = _sut.InitializeGame(room, "nl-BE", 10);

        // Assert
        state.Should().NotBeNull();
        state.RoomCode.Should().Be("TEST");
        state.Phase.Should().Be(QuizPhase.Question);
        state.QuestionNumber.Should().Be(0);
        state.TotalQuestions.Should().Be(10);
        state.Scoreboard.Should().HaveCount(3);
        state.Answers.Should().HaveCount(3);
        state.Scoreboard.Should().AllSatisfy(p => p.Score.Should().Be(0));
    }

    [Fact]
    public void InitializeGame_InitializesAnswersToNull()
    {
        // Arrange
        var (room, _) = CreateTestRoom(2);

        // Act
        var state = _sut.InitializeGame(room, "nl-BE", 5);

        // Assert
        state.Answers.Values.Should().AllSatisfy(a => a.Should().BeNull());
    }

    #endregion

    #region StartNewQuestion Tests

    [Fact]
    public void StartNewQuestion_LoadsQuestionFromBank()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        var now = DateTime.UtcNow;

        // Act
        var newState = _sut.StartNewQuestion(state, 15, now);

        // Assert
        newState.Should().NotBeNull();
        newState!.QuestionNumber.Should().Be(1);
        newState.QuestionId.Should().Be("q1");
        newState.QuestionText.Should().Be("What is 2 + 2?");
        newState.Options.Should().HaveCount(4);
        newState.CorrectOptionKey.Should().Be("B");
        newState.Phase.Should().Be(QuizPhase.Question);
        newState.UsedQuestionIds.Should().Contain("q1");
    }

    [Fact]
    public void StartNewQuestion_ClearsPreviousAnswers()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        state.Answers[playerIds[0]] = "A"; // Simulate previous answer

        // Act
        var newState = _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Assert
        newState!.Answers.Values.Should().AllSatisfy(a => a.Should().BeNull());
    }

    [Fact]
    public void StartNewQuestion_ReturnsNullWhenNoMoreQuestions()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        _questionBank.GetRandom(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<IEnumerable<string>?>())
            .Returns((QuizQuestion?)null);

        var state = _sut.InitializeGame(room, "nl-BE", 10);

        // Act
        var newState = _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Assert
        newState.Should().BeNull();
    }

    #endregion

    #region SubmitAnswer Tests

    [Fact]
    public void SubmitAnswer_RecordsAnswer()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        // Act
        var newState = _sut.SubmitAnswer(state, playerIds[0], "B");

        // Assert
        newState.Answers[playerIds[0]].Should().Be("B");
    }

    [Fact]
    public void SubmitAnswer_IsIdempotent_FirstAnswerWins()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.SubmitAnswer(state, playerIds[0], "A");
        _sut.SubmitAnswer(state, playerIds[0], "B"); // Should be ignored

        // Assert
        state.Answers[playerIds[0]].Should().Be("A");
    }

    [Fact]
    public void SubmitAnswer_IgnoresInvalidOption()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.SubmitAnswer(state, playerIds[0], "Z"); // Invalid option

        // Assert
        state.Answers[playerIds[0]].Should().BeNull();
    }

    #endregion

    #region RevealAnswer Tests

    [Fact]
    public void RevealAnswer_CalculatesScoresCorrectly()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(2);
        var question = CreateTestQuestion(); // Correct answer is "B"
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        _sut.SubmitAnswer(state, playerIds[0], "B"); // Correct
        _sut.SubmitAnswer(state, playerIds[1], "A"); // Wrong

        // Act
        _sut.RevealAnswer(state, 5, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.Reveal);
        
        var player1Score = state.Scoreboard.First(p => p.PlayerId == playerIds[0]);
        var player2Score = state.Scoreboard.First(p => p.PlayerId == playerIds[1]);

        player1Score.Score.Should().Be(100); // Got it right
        player1Score.AnsweredCorrectly.Should().BeTrue();
        player1Score.SelectedOption.Should().Be("B");

        player2Score.Score.Should().Be(0); // Got it wrong
        player2Score.AnsweredCorrectly.Should().BeFalse();
        player2Score.SelectedOption.Should().Be("A");
    }

    [Fact]
    public void RevealAnswer_UpdatesPositions()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        _sut.SubmitAnswer(state, playerIds[0], "B"); // Correct
        _sut.SubmitAnswer(state, playerIds[1], "B"); // Correct
        _sut.SubmitAnswer(state, playerIds[2], "A"); // Wrong

        // Act
        _sut.RevealAnswer(state, 5, DateTime.UtcNow);

        // Assert
        var winners = state.Scoreboard.Where(p => p.Score == 100).ToList();
        winners.Should().HaveCount(2);

        var loser = state.Scoreboard.First(p => p.Score == 0);
        loser.Position.Should().Be(3);
    }

    #endregion

    #region Phase Transition Tests

    [Fact]
    public void StartAnsweringPhase_SetsCorrectPhaseAndTime()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 3, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        // Act
        _sut.StartAnsweringPhase(state, 15, now);

        // Assert
        state.Phase.Should().Be(QuizPhase.Answering);
        state.PhaseEndsUtc.Should().BeCloseTo(now.AddSeconds(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ShowScoreboard_SetsCorrectPhase()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var state = _sut.InitializeGame(room, "nl-BE", 10);

        // Act
        _sut.ShowScoreboard(state, 5, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.Scoreboard);
    }

    [Fact]
    public void FinishGame_SetsFinishedPhase()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var state = _sut.InitializeGame(room, "nl-BE", 10);

        // Act
        _sut.FinishGame(state);

        // Assert
        state.Phase.Should().Be(QuizPhase.Finished);
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void AllPlayersAnswered_ReturnsTrueWhenAllAnswered()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(2);
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        _sut.SubmitAnswer(state, playerIds[0], "A");
        _sut.SubmitAnswer(state, playerIds[1], "B");

        // Act
        var result = _sut.AllPlayersAnswered(state, playerIds);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AllPlayersAnswered_ReturnsFalseWhenNotAllAnswered()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(2);
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);
        _sut.StartAnsweringPhase(state, 15, DateTime.UtcNow);

        _sut.SubmitAnswer(state, playerIds[0], "A");
        // Player 1 hasn't answered

        // Act
        var result = _sut.AllPlayersAnswered(state, playerIds);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasMoreQuestions_ReturnsTrueWhenMoreQuestionsAvailable()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var state = _sut.InitializeGame(room, "nl-BE", 10);
        state.QuestionNumber = 5;

        // Act
        var result = _sut.HasMoreQuestions(state);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasMoreQuestions_ReturnsFalseWhenNoMoreQuestions()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var state = _sut.InitializeGame(room, "nl-BE", 10);
        state.QuestionNumber = 10;

        // Act
        var result = _sut.HasMoreQuestions(state);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidOptionKey_ReturnsTrueForValidKey()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Act & Assert
        _sut.IsValidOptionKey(state, "A").Should().BeTrue();
        _sut.IsValidOptionKey(state, "B").Should().BeTrue();
        _sut.IsValidOptionKey(state, "C").Should().BeTrue();
        _sut.IsValidOptionKey(state, "D").Should().BeTrue();
        _sut.IsValidOptionKey(state, "a").Should().BeTrue(); // Case insensitive
    }

    [Fact]
    public void IsValidOptionKey_ReturnsFalseForInvalidKey()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Act & Assert
        _sut.IsValidOptionKey(state, "E").Should().BeFalse();
        _sut.IsValidOptionKey(state, "").Should().BeFalse();
        _sut.IsValidOptionKey(state, "  ").Should().BeFalse();
    }

    #endregion
}
