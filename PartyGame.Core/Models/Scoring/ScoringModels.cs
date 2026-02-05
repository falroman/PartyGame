namespace PartyGame.Core.Models.Scoring;

/// <summary>
/// Result of scoring calculation for a single player on a single question.
/// </summary>
public class QuestionScoreResult
{
    /// <summary>
    /// The player's ID.
    /// </summary>
    public required Guid PlayerId { get; set; }

    /// <summary>
    /// The player's rank based on answer time (1 = fastest).
    /// 0 if incorrect or no answer.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Base points earned (before boosters).
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Whether the answer was correct.
    /// </summary>
    public bool IsCorrect { get; set; }

    /// <summary>
    /// The answer submitted (option key or index).
    /// </summary>
    public string? SubmittedAnswer { get; set; }

    /// <summary>
    /// When the answer was submitted.
    /// </summary>
    public DateTime? AnswerTime { get; set; }

    /// <summary>
    /// Any bonus points (e.g., catch-up bonus).
    /// </summary>
    public int BonusPoints { get; set; }

    /// <summary>
    /// Final points after all bonuses.
    /// </summary>
    public int TotalPoints => Points + BonusPoints;
}

/// <summary>
/// Input for scoring calculation.
/// </summary>
public class ScoringInput
{
    /// <summary>
    /// The correct answer (option key for quiz, index for dictionary).
    /// </summary>
    public required string CorrectAnswer { get; set; }

    /// <summary>
    /// Player answers with their submission times.
    /// </summary>
    public required Dictionary<Guid, PlayerAnswer> Answers { get; set; }

    /// <summary>
    /// Current scores for catch-up bonus calculation.
    /// </summary>
    public Dictionary<Guid, int> CurrentScores { get; set; } = new();
}

/// <summary>
/// A player's submitted answer with timing.
/// </summary>
public class PlayerAnswer
{
    /// <summary>
    /// The answer submitted (option key or index as string).
    /// Null if no answer submitted.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// When the answer was submitted.
    /// </summary>
    public DateTime? SubmittedAtUtc { get; set; }
}
