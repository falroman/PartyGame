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

    private QuizQuestion CreateTestQuestion(string id = "q1", string category = "Math")
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
            Category = category
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

    private void SetupCategoryMock(params string[] categories)
    {
        _questionBank.GetRandomCategories(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<IEnumerable<string>?>())
            .Returns(categories.ToList());
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
        state.Phase.Should().Be(QuizPhase.CategorySelection);
        state.QuestionNumber.Should().Be(0);
        state.TotalQuestions.Should().Be(10);
        state.RoundNumber.Should().Be(0);
        state.Locale.Should().Be("nl-BE");
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

    #region StartNewRound Tests

    [Fact]
    public void StartNewRound_CreatesNewRoundWithCorrectLeader()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 9);

        // Act
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Assert
        state.RoundNumber.Should().Be(1);
        state.CurrentRound.Should().NotBeNull();
        state.CurrentRound!.RoundLeaderPlayerId.Should().NotBeEmpty();
        state.Phase.Should().Be(QuizPhase.CategorySelection);
        state.AvailableCategories.Should().HaveCount(3);
    }

    [Fact]
    public void StartNewRound_ExcludesUsedCategories()
    {
        // Arrange
        var (room, _) = CreateTestRoom(2);
        SetupCategoryMock("Science", "History", "Geography");

        var state = _sut.InitializeGame(room, "nl-BE", 9);
        state.UsedCategories.Add("Math"); // Previously used

        // Act
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Assert
        _questionBank.Received().GetRandomCategories(
            "nl-BE",
            3,
            Arg.Is<IEnumerable<string>>(x => x.Contains("Math")));
    }

    #endregion

    #region SelectRoundLeader Tests

    [Fact]
    public void SelectRoundLeader_PicksPlayerWithLowestScore()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", 9);

        // Give players different scores
        state.Scoreboard.First(p => p.PlayerId == playerIds[0]).Score = 200;
        state.Scoreboard.First(p => p.PlayerId == playerIds[1]).Score = 100;
        state.Scoreboard.First(p => p.PlayerId == playerIds[2]).Score = 300;

        // Act
        var leaderId = _sut.SelectRoundLeader(state);

        // Assert
        leaderId.Should().Be(playerIds[1]); // Player with 100 points
    }

    [Fact]
    public void SelectRoundLeader_BreaksTiesByOrder()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", 9);

        // All players have same score
        state.Scoreboard.ForEach(p => p.Score = 100);

        // Act
        var leaderId = _sut.SelectRoundLeader(state);

        // Assert
        leaderId.Should().Be(playerIds[0]); // First player in list
    }

    [Fact]
    public void SelectRoundLeader_AvoidsSameLeaderTwiceInRow()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", 9);

        // Player 0 has lowest score but was last leader
        state.Scoreboard.First(p => p.PlayerId == playerIds[0]).Score = 0;
        state.Scoreboard.First(p => p.PlayerId == playerIds[1]).Score = 100;
        state.Scoreboard.First(p => p.PlayerId == playerIds[2]).Score = 200;
        state.PreviousRoundLeaders.Add(playerIds[0]); // Was already leader

        // Act
        var leaderId = _sut.SelectRoundLeader(state);

        // Assert
        leaderId.Should().Be(playerIds[1]); // Next lowest scorer
    }

    #endregion

    #region SetRoundCategory Tests

    [Fact]
    public void SetRoundCategory_SetsCategoryAndMarksAsUsed()
    {
        // Arrange
        var (room, _) = CreateTestRoom(2);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 9);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Act
        _sut.SetRoundCategory(state, "Science");

        // Assert
        state.CurrentRound!.Category.Should().Be("Science");
        state.UsedCategories.Should().Contain("Science");
    }

    #endregion

    #region IsValidCategory Tests

    [Fact]
    public void IsValidCategory_ReturnsTrueForAvailableCategory()
    {
        // Arrange
        var (room, _) = CreateTestRoom(2);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 9);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Act & Assert
        _sut.IsValidCategory(state, "Math").Should().BeTrue();
        _sut.IsValidCategory(state, "Science").Should().BeTrue();
        _sut.IsValidCategory(state, "math").Should().BeTrue(); // Case insensitive
    }

    [Fact]
    public void IsValidCategory_ReturnsFalseForUnavailableCategory()
    {
        // Arrange
        var (room, _) = CreateTestRoom(2);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 9);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Act & Assert
        _sut.IsValidCategory(state, "Geography").Should().BeFalse();
        _sut.IsValidCategory(state, "").Should().BeFalse();
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
    public void StartNewQuestion_FiltersByRoundCategory()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Science");

        // Act
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Assert
        _questionBank.Received().GetRandom(
            "nl-BE",
            "Science",
            Arg.Any<int?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<IEnumerable<string>?>());
    }

    [Fact]
    public void StartNewQuestion_ClearsPreviousAnswers()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");

        // Act
        var newState = _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Assert
        newState.Should().BeNull();
    }

    [Fact]
    public void StartNewQuestion_IncrementsRoundQuestionIndex()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        var question = CreateTestQuestion();
        SetupQuestionBankMock(question);
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");

        // Act
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Assert
        state.CurrentRound!.CurrentQuestionIndex.Should().Be(1);
    }

    #endregion

    #region HasMoreQuestionsInRound Tests

    [Fact]
    public void HasMoreQuestionsInRound_ReturnsTrueWhenMoreQuestionsAvailable()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        state.CurrentRound!.CurrentQuestionIndex = 1;

        // Act
        var result = _sut.HasMoreQuestionsInRound(state);

        // Assert
        result.Should().BeTrue(); // 3 questions per round, only 1 done
    }

    [Fact]
    public void HasMoreQuestionsInRound_ReturnsFalseWhenRoundComplete()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        state.CurrentRound!.CurrentQuestionIndex = 3; // All 3 questions done

        // Act
        var result = _sut.HasMoreQuestionsInRound(state);

        // Assert
        result.Should().BeFalse();
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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

    [Fact]
    public void FinishGame_CompletesCurrentRound()
    {
        // Arrange
        var (room, _) = CreateTestRoom();
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);

        // Act
        _sut.FinishGame(state);

        // Assert
        state.CurrentRound!.IsCompleted.Should().BeTrue();
        state.CompletedRounds.Should().HaveCount(1);
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
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
        SetupCategoryMock("Math", "Science", "History");

        var state = _sut.InitializeGame(room, "nl-BE", 10);
        _sut.StartNewRound(state, 30, DateTime.UtcNow);
        _sut.SetRoundCategory(state, "Math");
        _sut.StartNewQuestion(state, 15, DateTime.UtcNow);

        // Act & Assert
        _sut.IsValidOptionKey(state, "E").Should().BeFalse();
        _sut.IsValidOptionKey(state, "").Should().BeFalse();
        _sut.IsValidOptionKey(state, "  ").Should().BeFalse();
    }

    #endregion
}
