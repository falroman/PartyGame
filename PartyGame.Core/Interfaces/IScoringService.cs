using PartyGame.Core.Models.Scoring;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Service for calculating question scores with ranking-based points.
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Calculates scores for all players on a question.
    /// Ranking-based scoring:
    /// - 1st correct: 100 points
    /// - 2nd correct: 90 points
    /// - 3rd correct: 85 points
    /// - 4th+ correct: 80 points
    /// - Incorrect: 0 points
    /// - Same timestamps: same rank/same points
    /// </summary>
    /// <param name="input">The scoring input with answers and correct answer.</param>
    /// <returns>List of score results for each player.</returns>
    List<QuestionScoreResult> CalculateQuestionScores(ScoringInput input);

    /// <summary>
    /// Calculates catch-up bonus for players in bottom half of scoreboard.
    /// </summary>
    /// <param name="playerId">The player to check.</param>
    /// <param name="currentScores">Current score standings.</param>
    /// <returns>Bonus points (0 if not eligible).</returns>
    int CalculateCatchUpBonus(Guid playerId, Dictionary<Guid, int> currentScores);
}
