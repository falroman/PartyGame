using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Scoring;

namespace PartyGame.Server.Services;

/// <summary>
/// Implements ranking-based scoring for quiz questions.
/// </summary>
public class ScoringService : IScoringService
{
    // Points per rank position
    private const int FirstPlacePoints = 100;
    private const int SecondPlacePoints = 90;
    private const int ThirdPlacePoints = 85;
    private const int DefaultCorrectPoints = 80;
    private const int CatchUpBonusPoints = 20;

    /// <inheritdoc />
    public List<QuestionScoreResult> CalculateQuestionScores(ScoringInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Answers);

        var results = new List<QuestionScoreResult>();

        // First, identify all correct answers with their times
        var correctAnswers = input.Answers
            .Where(a => a.Value.Answer != null && 
                       a.Value.Answer.Equals(input.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
            .Select(a => new
            {
                PlayerId = a.Key,
                Answer = a.Value.Answer,
                Time = a.Value.SubmittedAtUtc ?? DateTime.MaxValue
            })
            .OrderBy(a => a.Time)
            .ToList();

        // Group by time to handle ties (within 1ms tolerance)
        var rankedGroups = new List<List<Guid>>();
        DateTime? lastTime = null;
        List<Guid>? currentGroup = null;

        foreach (var answer in correctAnswers)
        {
            if (lastTime == null || (answer.Time - lastTime.Value).TotalMilliseconds > 1)
            {
                currentGroup = new List<Guid>();
                rankedGroups.Add(currentGroup);
            }
            currentGroup!.Add(answer.PlayerId);
            lastTime = answer.Time;
        }

        // Assign ranks and points
        var playerRanks = new Dictionary<Guid, int>();
        var playerPoints = new Dictionary<Guid, int>();
        int currentRank = 1;

        foreach (var group in rankedGroups)
        {
            int points = GetPointsForRank(currentRank);
            foreach (var playerId in group)
            {
                playerRanks[playerId] = currentRank;
                playerPoints[playerId] = points;
            }
            currentRank += group.Count;
        }

        // Create results for all players
        foreach (var (playerId, playerAnswer) in input.Answers)
        {
            var isCorrect = playerAnswer.Answer != null && 
                           playerAnswer.Answer.Equals(input.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

            var result = new QuestionScoreResult
            {
                PlayerId = playerId,
                IsCorrect = isCorrect,
                SubmittedAnswer = playerAnswer.Answer,
                AnswerTime = playerAnswer.SubmittedAtUtc,
                Rank = isCorrect ? playerRanks.GetValueOrDefault(playerId, 0) : 0,
                Points = isCorrect ? playerPoints.GetValueOrDefault(playerId, 0) : 0,
                BonusPoints = 0
            };

            // Calculate catch-up bonus if applicable
            if (isCorrect && input.CurrentScores.Count > 0)
            {
                result.BonusPoints = CalculateCatchUpBonus(playerId, input.CurrentScores);
            }

            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public int CalculateCatchUpBonus(Guid playerId, Dictionary<Guid, int> currentScores)
    {
        if (currentScores.Count < 2)
            return 0;

        if (!currentScores.TryGetValue(playerId, out var playerScore))
            return 0;

        var sortedScores = currentScores.Values.OrderBy(s => s).ToList();
        var medianIndex = sortedScores.Count / 2;
        var medianScore = sortedScores[medianIndex];

        // Player in bottom half gets catch-up bonus
        if (playerScore <= medianScore)
        {
            return CatchUpBonusPoints;
        }

        return 0;
    }

    /// <summary>
    /// Gets points for a given rank position.
    /// </summary>
    private static int GetPointsForRank(int rank)
    {
        return rank switch
        {
            1 => FirstPlacePoints,
            2 => SecondPlacePoints,
            3 => ThirdPlacePoints,
            _ => DefaultCorrectPoints
        };
    }
}
