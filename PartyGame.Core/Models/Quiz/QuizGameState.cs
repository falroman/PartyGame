using PartyGame.Core.Enums;

namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents the current state of a quiz game.
/// This is the server-authoritative state that drives the game.
/// </summary>
public class QuizGameState
{
    /// <summary>
    /// The room code this game belongs to.
    /// </summary>
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>
    /// Current phase of the quiz.
    /// </summary>
    public QuizPhase Phase { get; set; } = QuizPhase.CategorySelection;

    /// <summary>
    /// Current question number (1-based, across all rounds).
    /// </summary>
    public int QuestionNumber { get; set; } = 0;

    /// <summary>
    /// Total number of questions in the game.
    /// </summary>
    public int TotalQuestions { get; set; } = 10;

    /// <summary>
    /// Current round number (1-based).
    /// </summary>
    public int RoundNumber { get; set; } = 0;

    /// <summary>
    /// The current active round.
    /// </summary>
    public GameRound? CurrentRound { get; set; }

    /// <summary>
    /// List of completed rounds.
    /// </summary>
    public List<GameRound> CompletedRounds { get; set; } = new();

    /// <summary>
    /// Categories that have already been used in previous rounds.
    /// </summary>
    public HashSet<string> UsedCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Categories available for selection in the current CategorySelection phase.
    /// </summary>
    public List<string> AvailableCategories { get; set; } = new();

    /// <summary>
    /// ID of the current question.
    /// </summary>
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>
    /// Text of the current question.
    /// </summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Answer options for the current question.
    /// </summary>
    public List<QuizOptionState> Options { get; set; } = new();

    /// <summary>
    /// The correct option key. Only populated in Reveal/Scoreboard phases.
    /// </summary>
    public string? CorrectOptionKey { get; set; }

    /// <summary>
    /// Explanation for the correct answer. Only populated in Reveal phase.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Player answers: PlayerId -> OptionKey (null if not answered).
    /// </summary>
    public Dictionary<Guid, string?> Answers { get; set; } = new();

    /// <summary>
    /// When the current phase ends. Used for countdown timers.
    /// </summary>
    public DateTime PhaseEndsUtc { get; set; }

    /// <summary>
    /// Current scores snapshot.
    /// </summary>
    public List<PlayerScoreState> Scoreboard { get; set; } = new();

    /// <summary>
    /// IDs of questions already used in this game (to prevent repeats).
    /// </summary>
    public HashSet<string> UsedQuestionIds { get; set; } = new();

    /// <summary>
    /// Points awarded for a correct answer.
    /// </summary>
    public int PointsPerCorrectAnswer { get; set; } = 100;

    /// <summary>
    /// Player IDs who have already been round leaders (to ensure fairness).
    /// </summary>
    public List<Guid> PreviousRoundLeaders { get; set; } = new();

    /// <summary>
    /// Locale for questions (e.g., "nl-BE").
    /// </summary>
    public string Locale { get; set; } = "nl-BE";
}

/// <summary>
/// Represents an answer option in quiz state.
/// </summary>
public class QuizOptionState
{
    /// <summary>
    /// Option key (A, B, C, D).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Option text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Represents a player's score in the scoreboard.
/// </summary>
public class PlayerScoreState
{
    /// <summary>
    /// Player's unique ID.
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Player's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Player's current score.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Position on the scoreboard (1-based).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Whether the player answered the current question correctly (in Reveal phase).
    /// </summary>
    public bool? AnsweredCorrectly { get; set; }

    /// <summary>
    /// The option the player selected (in Reveal phase).
    /// </summary>
    public string? SelectedOption { get; set; }
}
