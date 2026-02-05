using FluentAssertions;
using NSubstitute;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Ranking;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Services;
using Xunit;

namespace PartyGame.Tests.Unit;

public class RankingStarsEngineTests
{
    private readonly IQuizQuestionBank _questionBank;
    private readonly IQuizGameEngine _sut;

    public RankingStarsEngineTests()
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

    private RankingPrompt CreateTestPrompt(string id = "rs_001")
    {
        return new RankingPrompt
        {
            Id = id,
            Prompt = "Who would survive longest in a zombie apocalypse?"
        };
    }

    private static List<RoundType> CreateRoundPlanWithRanking()
    {
        return new List<RoundType>
        {
            RoundType.CategoryQuiz,
            RoundType.RankingStars,
            RoundType.DictionaryGame
        };
    }

    #region StartRankingRound Tests

    [Fact]
    public void StartRankingRound_CreatesRoundWithCorrectType()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        state.PlannedRoundIndex = 0; // At CategoryQuiz, next will be RankingStars

        // Act
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);

        // Assert
        state.CurrentRound.Should().NotBeNull();
        state.CurrentRound!.Type.Should().Be(RoundType.RankingStars);
        state.RankingPromptIndex.Should().Be(0);
    }

    [Fact]
    public void StartRankingRound_HasNoRoundLeader()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());

        // Act
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);

        // Assert
        state.CurrentRound!.RoundLeaderPlayerId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void StartRankingRound_IncrementsPlannedRoundIndex()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        state.PlannedRoundIndex = 0;

        // Act
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);

        // Assert
        state.PlannedRoundIndex.Should().Be(1);
    }

    [Fact]
    public void StartRankingRound_ResetsVotes()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        
        // Simulate previous votes
        state.RankingVotes[playerIds[0]] = playerIds[1];
        state.RankingVotes[playerIds[1]] = playerIds[0];

        // Act
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);

        // Assert
        state.RankingVotes.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }

    #endregion

    #region StartRankingPrompt Tests

    [Fact]
    public void StartRankingPrompt_SetsPhaseToRankingPrompt()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        var prompt = CreateTestPrompt();

        // Act
        _sut.StartRankingPrompt(state, prompt, 2, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.RankingPrompt);
        state.RankingPromptId.Should().Be(prompt.Id);
        state.RankingPromptText.Should().Be(prompt.Prompt);
    }

    [Fact]
    public void StartRankingPrompt_IncrementsPromptIndex()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        var prompt = CreateTestPrompt();

        // Act
        _sut.StartRankingPrompt(state, prompt, 2, DateTime.UtcNow);

        // Assert
        state.RankingPromptIndex.Should().Be(1);
    }

    [Fact]
    public void StartRankingPrompt_TracksUsedPromptId()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        var prompt = CreateTestPrompt("unique_prompt_id");

        // Act
        _sut.StartRankingPrompt(state, prompt, 2, DateTime.UtcNow);

        // Assert
        state.UsedRankingPromptIds.Should().Contain("unique_prompt_id");
    }

    [Fact]
    public void StartRankingPrompt_ClearsVotes()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        
        // Simulate previous votes
        state.RankingVotes[playerIds[0]] = playerIds[1];
        
        var prompt = CreateTestPrompt();

        // Act
        _sut.StartRankingPrompt(state, prompt, 2, DateTime.UtcNow);

        // Assert
        state.RankingVotes.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }

    #endregion

    #region StartRankingVotingPhase Tests

    [Fact]
    public void StartRankingVotingPhase_SetsCorrectPhase()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);

        // Act
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.RankingVoting);
    }

    #endregion

    #region SubmitRankingVote Tests

    [Fact]
    public void SubmitRankingVote_RecordsVote()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], DateTime.UtcNow);

        // Assert
        state.RankingVotes[playerIds[0]].Should().Be(playerIds[1]);
    }

    [Fact]
    public void SubmitRankingVote_RecordsTimestamp()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        var voteTime = DateTime.UtcNow;

        // Act
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], voteTime);

        // Assert
        state.RankingVoteTimes[playerIds[0]].Should().Be(voteTime);
    }

    [Fact]
    public void SubmitRankingVote_IsIdempotent()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Act - Submit twice
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[2], DateTime.UtcNow.AddSeconds(1));

        // Assert - First vote wins
        state.RankingVotes[playerIds[0]].Should().Be(playerIds[1]);
    }

    [Fact]
    public void SubmitRankingVote_RejectsSelfVote()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[0], DateTime.UtcNow);

        // Assert
        state.RankingVotes[playerIds[0]].Should().BeNull();
    }

    [Fact]
    public void SubmitRankingVote_RejectsNonExistentTarget()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.SubmitRankingVote(state, playerIds[0], Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        state.RankingVotes[playerIds[0]].Should().BeNull();
    }

    #endregion

    #region IsValidRankingVote Tests

    [Fact]
    public void IsValidRankingVote_ReturnsTrueForValidVote()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());

        // Act & Assert
        _sut.IsValidRankingVote(state, playerIds[0], playerIds[1]).Should().BeTrue();
    }

    [Fact]
    public void IsValidRankingVote_ReturnsFalseForSelfVote()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());

        // Act & Assert
        _sut.IsValidRankingVote(state, playerIds[0], playerIds[0]).Should().BeFalse();
    }

    [Fact]
    public void IsValidRankingVote_ReturnsFalseForNonExistentTarget()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());

        // Act & Assert
        _sut.IsValidRankingVote(state, playerIds[0], Guid.NewGuid()).Should().BeFalse();
    }

    #endregion

    #region CalculateRankingResult Tests

    [Fact]
    public void CalculateRankingResult_FindsWinnerWithMostVotes()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        
        // Player 0 gets 2 votes, Player 1 gets 1 vote
        state.RankingVotes[playerIds[1]] = playerIds[0];
        state.RankingVotes[playerIds[2]] = playerIds[0];
        state.RankingVotes[playerIds[3]] = playerIds[1];

        // Act
        var result = _sut.CalculateRankingResult(state);

        // Assert
        result.WinnerPlayerIds.Should().ContainSingle(id => id == playerIds[0]);
        result.MaxVotes.Should().Be(2);
        result.VoteCounts[playerIds[0]].Should().Be(2);
        result.VoteCounts[playerIds[1]].Should().Be(1);
    }

    [Fact]
    public void CalculateRankingResult_HandlesTie()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        
        // Player 0 and Player 1 both get 2 votes each
        state.RankingVotes[playerIds[2]] = playerIds[0];
        state.RankingVotes[playerIds[3]] = playerIds[0];
        state.RankingVotes[playerIds[0]] = playerIds[1];
        state.RankingVotes[playerIds[1]] = playerIds[1]; // Self-vote shouldn't count but we're setting directly

        // Override with valid scenario: P2->P0, P3->P1, P0->P1, P1->P0
        state.RankingVotes[playerIds[0]] = playerIds[1];
        state.RankingVotes[playerIds[1]] = playerIds[0];
        state.RankingVotes[playerIds[2]] = playerIds[0];
        state.RankingVotes[playerIds[3]] = playerIds[1];

        // Act
        var result = _sut.CalculateRankingResult(state);

        // Assert - Both P0 and P1 have 2 votes
        result.WinnerPlayerIds.Should().HaveCount(2);
        result.WinnerPlayerIds.Should().Contain(playerIds[0]);
        result.WinnerPlayerIds.Should().Contain(playerIds[1]);
        result.MaxVotes.Should().Be(2);
    }

    [Fact]
    public void CalculateRankingResult_FindsCorrectVoters()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        
        // Player 0 wins with votes from P1 and P2
        state.RankingVotes[playerIds[1]] = playerIds[0];
        state.RankingVotes[playerIds[2]] = playerIds[0];
        state.RankingVotes[playerIds[3]] = playerIds[1];

        // Act
        var result = _sut.CalculateRankingResult(state);

        // Assert
        result.CorrectVoters.Should().HaveCount(2);
        result.CorrectVoters.Should().Contain(playerIds[1]);
        result.CorrectVoters.Should().Contain(playerIds[2]);
        result.CorrectVoters.Should().NotContain(playerIds[3]);
    }

    #endregion

    #region RevealRankingVotes Tests

    [Fact]
    public void RevealRankingVotes_AwardsStarPoints()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        // Player 0 gets all votes
        _sut.SubmitRankingVote(state, playerIds[1], playerIds[0], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[2], playerIds[0], DateTime.UtcNow);

        // Act
        _sut.RevealRankingVotes(state, 5, DateTime.UtcNow);

        // Assert
        var winner = state.Scoreboard.First(p => p.PlayerId == playerIds[0]);
        winner.IsRankingStar.Should().BeTrue();
        winner.Score.Should().BeGreaterOrEqualTo(QuizGameState.RankingStarPoints);
    }

    [Fact]
    public void RevealRankingVotes_AwardsCorrectVotePoints()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4); // 4 players to have clear winner
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        
        // Give everyone high starting score to avoid catch-up bonus
        foreach (var player in state.Scoreboard)
        {
            player.Score = 1000;
        }
        
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        // Player 0 wins with 2 votes
        // Player 1 and Player 2 vote for Player 0 (correct)
        // Player 3 votes for Player 1 (wrong)
        _sut.SubmitRankingVote(state, playerIds[1], playerIds[0], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[2], playerIds[0], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[3], playerIds[1], DateTime.UtcNow);

        // Act
        _sut.RevealRankingVotes(state, 5, DateTime.UtcNow);

        // Assert
        var correctVoter1 = state.Scoreboard.First(p => p.PlayerId == playerIds[1]);
        var correctVoter2 = state.Scoreboard.First(p => p.PlayerId == playerIds[2]);
        var wrongVoter = state.Scoreboard.First(p => p.PlayerId == playerIds[3]);
        
        // Correct voters get points
        correctVoter1.PointsEarned.Should().Be(QuizGameState.RankingCorrectVotePoints);
        correctVoter1.AnsweredCorrectly.Should().BeTrue();
        
        correctVoter2.PointsEarned.Should().Be(QuizGameState.RankingCorrectVotePoints);
        correctVoter2.AnsweredCorrectly.Should().BeTrue();
        
        // Wrong voter gets 0 points
        wrongVoter.PointsEarned.Should().Be(0);
        wrongVoter.AnsweredCorrectly.Should().BeFalse();
    }

    [Fact]
    public void RevealRankingVotes_SetsPhaseToRankingReveal()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);

        // Act
        _sut.RevealRankingVotes(state, 5, DateTime.UtcNow);

        // Assert
        state.Phase.Should().Be(QuizPhase.RankingReveal);
    }

    [Fact]
    public void RevealRankingVotes_AwardsCatchUpBonus()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(4);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        
        // Set up scores: P0,P1 high, P2,P3 low
        state.Scoreboard.First(p => p.PlayerId == playerIds[0]).Score = 500;
        state.Scoreboard.First(p => p.PlayerId == playerIds[1]).Score = 400;
        state.Scoreboard.First(p => p.PlayerId == playerIds[2]).Score = 100;
        state.Scoreboard.First(p => p.PlayerId == playerIds[3]).Score = 0;

        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        // P1 (high score) wins, but P3 (low score) voted correctly
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[2], playerIds[0], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[3], playerIds[1], DateTime.UtcNow);

        // Act
        _sut.RevealRankingVotes(state, 5, DateTime.UtcNow);

        // Assert - P3 should get catch-up bonus
        var player3 = state.Scoreboard.First(p => p.PlayerId == playerIds[3]);
        player3.PointsEarned.Should().Be(
            QuizGameState.RankingCorrectVotePoints + QuizGameState.RankingCatchUpBonusPoints);
    }

    [Fact]
    public void RevealRankingVotes_StoresResult()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        _sut.SubmitRankingVote(state, playerIds[1], playerIds[0], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[2], playerIds[0], DateTime.UtcNow);

        // Act
        _sut.RevealRankingVotes(state, 5, DateTime.UtcNow);

        // Assert
        state.RankingResult.Should().NotBeNull();
        state.RankingResult!.WinnerPlayerIds.Should().Contain(playerIds[0]);
    }

    #endregion

    #region HasMoreRankingPrompts Tests

    [Fact]
    public void HasMoreRankingPrompts_TrueWhenLessThanThree()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        state.RankingPromptIndex = 1;

        // Act & Assert
        _sut.HasMoreRankingPrompts(state).Should().BeTrue();
    }

    [Fact]
    public void HasMoreRankingPrompts_FalseWhenThreeCompleted()
    {
        // Arrange
        var (room, _) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        state.RankingPromptIndex = 3;

        // Act & Assert
        _sut.HasMoreRankingPrompts(state).Should().BeFalse();
    }

    #endregion

    #region AllRankingVoted Tests

    [Fact]
    public void AllRankingVoted_ReturnsTrueWhenAllVoted()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[1], playerIds[2], DateTime.UtcNow);
        _sut.SubmitRankingVote(state, playerIds[2], playerIds[0], DateTime.UtcNow);

        // Act & Assert
        _sut.AllRankingVoted(state, playerIds).Should().BeTrue();
    }

    [Fact]
    public void AllRankingVoted_ReturnsFalseWhenNotAllVoted()
    {
        // Arrange
        var (room, playerIds) = CreateTestRoom(3);
        var state = _sut.InitializeGame(room, "nl-BE", CreateRoundPlanWithRanking());
        _sut.StartRankingRound(state, 2, DateTime.UtcNow);
        _sut.StartRankingPrompt(state, CreateTestPrompt(), 2, DateTime.UtcNow);
        _sut.StartRankingVotingPhase(state, 15, DateTime.UtcNow);
        
        _sut.SubmitRankingVote(state, playerIds[0], playerIds[1], DateTime.UtcNow);
        // Other players haven't voted

        // Act & Assert
        _sut.AllRankingVoted(state, playerIds).Should().BeFalse();
    }

    #endregion
}
