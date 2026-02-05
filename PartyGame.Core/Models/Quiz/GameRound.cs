using PartyGame.Core.Enums;

namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents a single round in a quiz game.
/// Each round has 3 questions from a single category.
/// </summary>
public class GameRound
{
    /// <summary>
    /// Unique identifier for this round.
    /// </summary>
    public Guid RoundId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The type of round (e.g., CategoryQuiz).
    /// </summary>
    public RoundType Type { get; set; } = RoundType.CategoryQuiz;

    /// <summary>
    /// The category for this round's questions.
    /// Set when the round leader makes their selection.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The player ID of the round leader who selects the category.
    /// </summary>
    public Guid RoundLeaderPlayerId { get; set; }

    /// <summary>
    /// Current question index within the round (0-based, max 2 for 3 questions).
    /// </summary>
    public int CurrentQuestionIndex { get; set; } = 0;

    /// <summary>
    /// Number of questions per round.
    /// </summary>
    public const int QuestionsPerRound = 3;

    /// <summary>
    /// Whether this round has been completed (all 3 questions answered).
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Creates a new round with the specified leader.
    /// </summary>
    public static GameRound Create(Guid roundLeaderPlayerId)
    {
        return new GameRound
        {
            RoundId = Guid.NewGuid(),
            Type = RoundType.CategoryQuiz,
            RoundLeaderPlayerId = roundLeaderPlayerId,
            CurrentQuestionIndex = 0,
            IsCompleted = false
        };
    }
}
