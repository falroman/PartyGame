using PartyGame.Core.Enums;
using PartyGame.Core.Models.Dictionary;
using PartyGame.Core.Models.Ranking;

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
    /// Total number of questions in the game (excluding dictionary round).
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

    #region Round Planning

    /// <summary>
    /// The planned sequence of round types for this game.
    /// DictionaryGame is always last.
    /// </summary>
    public List<RoundType> PlannedRounds { get; set; } = new();

    /// <summary>
    /// Current index in PlannedRounds (0-based).
    /// </summary>
    public int PlannedRoundIndex { get; set; } = -1;

    /// <summary>
    /// Gets whether we're in the final dictionary round.
    /// </summary>
    public bool IsInFinalDictionaryRound => CurrentRound?.Type == RoundType.DictionaryGame;

    #endregion

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

    #region Dictionary Game State

    /// <summary>
    /// Current dictionary question (only set during DictionaryGame round).
    /// </summary>
    public DictionaryQuestion? DictionaryQuestion { get; set; }

    /// <summary>
    /// Dictionary answers: PlayerId -> OptionIndex (0-3), null if not answered.
    /// </summary>
    public Dictionary<Guid, int?> DictionaryAnswers { get; set; } = new();

    /// <summary>
    /// Dictionary answer timestamps: PlayerId -> UTC time when answer was submitted.
    /// Used for speed bonus calculation.
    /// </summary>
    public Dictionary<Guid, DateTime> DictionaryAnswerTimes { get; set; } = new();

    /// <summary>
    /// Words already used in this game's dictionary round (to prevent repeats).
    /// </summary>
    public HashSet<string> UsedDictionaryWords { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Current word index within the dictionary round (1-based, 1-3).
    /// </summary>
    public int DictionaryWordIndex { get; set; } = 0;

    /// <summary>
    /// Number of words per dictionary round.
    /// </summary>
    public const int DictionaryWordsPerRound = 3;

    /// <summary>
    /// Points for correct dictionary answer.
    /// </summary>
    public const int DictionaryCorrectPoints = 1000;

    /// <summary>
    /// Bonus points for fastest correct answer.
    /// </summary>
    public const int DictionarySpeedBonusPoints = 250;

    /// <summary>
    /// Extra catch-up bonus for players in bottom half.
    /// </summary>
    public const int DictionaryCatchUpBonusPoints = 100;

    #endregion

    #region Ranking Stars State

    /// <summary>
    /// Current ranking prompt ID.
    /// </summary>
    public string? RankingPromptId { get; set; }

    /// <summary>
    /// Current ranking prompt text.
    /// </summary>
    public string? RankingPromptText { get; set; }

    /// <summary>
    /// Ranking votes: VoterPlayerId -> VotedForPlayerId (null if not voted).
    /// </summary>
    public Dictionary<Guid, Guid?> RankingVotes { get; set; } = new();

    /// <summary>
    /// Ranking vote timestamps: VoterPlayerId -> UTC time when vote was submitted.
    /// </summary>
    public Dictionary<Guid, DateTime> RankingVoteTimes { get; set; } = new();

    /// <summary>
    /// IDs of prompts already used in this game's ranking round.
    /// </summary>
    public HashSet<string> UsedRankingPromptIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Current prompt index within the ranking round (1-based, 1-3).
    /// </summary>
    public int RankingPromptIndex { get; set; } = 0;

    /// <summary>
    /// Last computed ranking vote result (for reveal phase).
    /// </summary>
    public RankingVoteResult? RankingResult { get; set; }

    /// <summary>
    /// Number of prompts per ranking round.
    /// </summary>
    public const int RankingPromptsPerRound = 3;

    /// <summary>
    /// Points for being the "Star" (most voted player).
    /// </summary>
    public const int RankingStarPoints = 500;

    /// <summary>
    /// Points for voting for the winning player.
    /// </summary>
    public const int RankingCorrectVotePoints = 250;

    /// <summary>
    /// Catch-up bonus for players in bottom 50% who voted correctly.
    /// </summary>
    public const int RankingCatchUpBonusPoints = 50;

    #endregion
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

    /// <summary>
    /// Points earned in the current question (for delta display).
    /// </summary>
    public int PointsEarned { get; set; } = 0;

    /// <summary>
    /// Whether this player got the speed bonus (fastest correct answer).
    /// </summary>
    public bool GotSpeedBonus { get; set; } = false;

    /// <summary>
    /// Whether this player is the "Star" (most voted) in Ranking Stars.
    /// </summary>
    public bool IsRankingStar { get; set; } = false;

    /// <summary>
    /// Number of votes received in Ranking Stars.
    /// </summary>
    public int RankingVotesReceived { get; set; } = 0;
}
