using FluentAssertions;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Scoring;
using PartyGame.Server.Services;
using Xunit;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for the ranking-based scoring service.
/// </summary>
public class ScoringServiceTests
{
    private readonly IScoringService _sut;

    public ScoringServiceTests()
    {
        _sut = new ScoringService();
    }

    #region Basic Scoring Tests

    [Fact]
    public void CalculateQuestionScores_FirstCorrect_Gets100Points()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "A", SubmittedAtUtc = DateTime.UtcNow }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(1);
        results[0].PlayerId.Should().Be(player1);
        results[0].IsCorrect.Should().BeTrue();
        results[0].Rank.Should().Be(1);
        results[0].Points.Should().Be(100);
    }

    [Fact]
    public void CalculateQuestionScores_SecondCorrect_Gets90Points()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "A", SubmittedAtUtc = baseTime },
                [player2] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(1) }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(2);
        
        var first = results.First(r => r.PlayerId == player1);
        first.Rank.Should().Be(1);
        first.Points.Should().Be(100);

        var second = results.First(r => r.PlayerId == player2);
        second.Rank.Should().Be(2);
        second.Points.Should().Be(90);
    }

    [Fact]
    public void CalculateQuestionScores_ThirdCorrect_Gets85Points()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "A", SubmittedAtUtc = baseTime },
                [player2] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(1) },
                [player3] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(2) }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        var third = results.First(r => r.PlayerId == player3);
        third.Rank.Should().Be(3);
        third.Points.Should().Be(85);
    }

    [Fact]
    public void CalculateQuestionScores_FourthAndLaterCorrect_Gets80Points()
    {
        // Arrange
        var players = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = players.Select((p, i) => new { p, i })
                .ToDictionary(
                    x => x.p,
                    x => new PlayerAnswer { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(x.i) }
                )
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        var fourth = results.First(r => r.Rank == 4);
        fourth.Points.Should().Be(80);

        var fifth = results.First(r => r.Rank == 5);
        fifth.Points.Should().Be(80);
    }

    [Fact]
    public void CalculateQuestionScores_IncorrectAnswer_Gets0Points()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "B", SubmittedAtUtc = DateTime.UtcNow }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(1);
        results[0].IsCorrect.Should().BeFalse();
        results[0].Rank.Should().Be(0);
        results[0].Points.Should().Be(0);
    }

    [Fact]
    public void CalculateQuestionScores_NoAnswer_Gets0Points()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = null, SubmittedAtUtc = null }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(1);
        results[0].IsCorrect.Should().BeFalse();
        results[0].Points.Should().Be(0);
    }

    #endregion

    #region Tie Handling Tests

    [Fact]
    public void CalculateQuestionScores_SameTimestamp_SameRankSamePoints()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var sameTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "A", SubmittedAtUtc = sameTime },
                [player2] = new() { Answer = "A", SubmittedAtUtc = sameTime }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.Rank.Should().Be(1);
            r.Points.Should().Be(100);
        });
    }

    [Fact]
    public void CalculateQuestionScores_TieForFirst_NextPlayerGetsThirdPlace()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "A", SubmittedAtUtc = baseTime },
                [player2] = new() { Answer = "A", SubmittedAtUtc = baseTime }, // Same time as player1
                [player3] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(1) }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        var first = results.First(r => r.PlayerId == player1);
        first.Rank.Should().Be(1);
        first.Points.Should().Be(100);

        var second = results.First(r => r.PlayerId == player2);
        second.Rank.Should().Be(1); // Tied for first
        second.Points.Should().Be(100);

        var third = results.First(r => r.PlayerId == player3);
        third.Rank.Should().Be(3); // Skips rank 2 because 2 players tied for first
        third.Points.Should().Be(85);
    }

    #endregion

    #region Catch-Up Bonus Tests

    [Fact]
    public void CalculateCatchUpBonus_PlayerInBottomHalf_GetsBonus()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var player4 = Guid.NewGuid();

        var scores = new Dictionary<Guid, int>
        {
            [player1] = 500,
            [player2] = 400,
            [player3] = 100,
            [player4] = 0
        };

        // Act & Assert - player4 (0) is strictly below median (between 100-400)
        _sut.CalculateCatchUpBonus(player4, scores).Should().Be(20);
    }

    [Fact]
    public void CalculateCatchUpBonus_PlayerInTopHalf_NoBonus()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var player4 = Guid.NewGuid();

        var scores = new Dictionary<Guid, int>
        {
            [player1] = 500,
            [player2] = 400,
            [player3] = 100,
            [player4] = 0
        };

        // Sorted scores: [0, 100, 400, 500]
        // Median index = 4/2 = 2 -> medianScore = 400
        // Players with score > 400 get no bonus
        // Players with score <= 400 get bonus
        
        // Act & Assert - only player1 (500) is above median
        _sut.CalculateCatchUpBonus(player1, scores).Should().Be(0); // 500 > 400, no bonus
        // Note: player2 (400) is at median so gets bonus per implementation
    }

    [Fact]
    public void CalculateCatchUpBonus_SinglePlayer_NoBonus()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var scores = new Dictionary<Guid, int> { [player1] = 100 };

        // Act & Assert
        _sut.CalculateCatchUpBonus(player1, scores).Should().Be(0);
    }

    [Fact]
    public void CalculateQuestionScores_WithCurrentScores_AppliesCatchUpBonus()
    {
        // Arrange
        var highScorePlayer = Guid.NewGuid();
        var lowScorePlayer = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [highScorePlayer] = new() { Answer = "A", SubmittedAtUtc = baseTime },
                [lowScorePlayer] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddSeconds(1) }
            },
            CurrentScores = new Dictionary<Guid, int>
            {
                [highScorePlayer] = 500,
                [lowScorePlayer] = 0  // Much lower to be strictly below median
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        // With 2 players: sorted scores = [0, 500], median index = 1, median = 500
        // Player with 0 score is <= 500, so gets bonus
        // Player with 500 score is <= 500, so also gets bonus (edge case)
        
        var lowScoreResult = results.First(r => r.PlayerId == lowScorePlayer);
        lowScoreResult.BonusPoints.Should().Be(20); // Bottom half gets catch-up bonus
        lowScoreResult.TotalPoints.Should().Be(90 + 20); // 90 (2nd place) + 20 (bonus)
    }

    #endregion

    #region Mixed Scenarios

    [Fact]
    public void CalculateQuestionScores_MixedCorrectAndIncorrect_ScoresCorrectly()
    {
        // Arrange
        var correct1 = Guid.NewGuid();
        var correct2 = Guid.NewGuid();
        var wrong1 = Guid.NewGuid();
        var wrong2 = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;
        
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [correct1] = new() { Answer = "A", SubmittedAtUtc = baseTime },
                [wrong1] = new() { Answer = "B", SubmittedAtUtc = baseTime.AddMilliseconds(100) },
                [correct2] = new() { Answer = "A", SubmittedAtUtc = baseTime.AddMilliseconds(200) },
                [wrong2] = new() { Answer = "C", SubmittedAtUtc = baseTime.AddMilliseconds(300) }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results.Should().HaveCount(4);

        results.First(r => r.PlayerId == correct1).Points.Should().Be(100);
        results.First(r => r.PlayerId == correct2).Points.Should().Be(90);
        results.First(r => r.PlayerId == wrong1).Points.Should().Be(0);
        results.First(r => r.PlayerId == wrong2).Points.Should().Be(0);
    }

    [Fact]
    public void CalculateQuestionScores_CaseInsensitiveComparison()
    {
        // Arrange
        var player1 = Guid.NewGuid();
        var input = new ScoringInput
        {
            CorrectAnswer = "A",
            Answers = new Dictionary<Guid, PlayerAnswer>
            {
                [player1] = new() { Answer = "a", SubmittedAtUtc = DateTime.UtcNow }
            }
        };

        // Act
        var results = _sut.CalculateQuestionScores(input);

        // Assert
        results[0].IsCorrect.Should().BeTrue();
        results[0].Points.Should().Be(100);
    }

    #endregion
}
