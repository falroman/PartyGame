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
    public QuizPhase Phase { get; set; } = QuizPhase.Question;

    /// <summary>
    /// Current question number (1-based).
    /// </summary>
    public int QuestionNumber { get; set; } = 1;

    /// <summary>
    /// Total number of questions in the game.
    /// </summary>
    public int TotalQuestions { get; set; } = 10;

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
