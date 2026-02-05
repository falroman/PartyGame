using PartyGame.Core.Enums;

namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents a single round in a quiz game.
/// Each round has 3 questions from a single category (CategoryQuiz) 
/// or 3 words (DictionaryGame).
/// </summary>
public class GameRound
{
    /// <summary>
    /// Unique identifier for this round.
    /// </summary>
    public Guid RoundId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The type of round (e.g., CategoryQuiz, DictionaryGame).
    /// </summary>
    public RoundType Type { get; set; } = RoundType.CategoryQuiz;

    /// <summary>
    /// The category for this round's questions.
    /// Set when the round leader makes their selection.
    /// Only used for CategoryQuiz rounds.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The player ID of the round leader who selects the category.
    /// Not used for DictionaryGame rounds.
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
    /// Creates a new CategoryQuiz round with the specified leader.
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

    /// <summary>
    /// Creates a new DictionaryGame round.
    /// </summary>
    public static GameRound CreateDictionaryRound()
    {
        return new GameRound
        {
            RoundId = Guid.NewGuid(),
            Type = RoundType.DictionaryGame,
            Category = "Woordenboekspel",
            RoundLeaderPlayerId = Guid.Empty, // No leader for dictionary round
            CurrentQuestionIndex = 0,
            IsCompleted = false
        };
    }
}
