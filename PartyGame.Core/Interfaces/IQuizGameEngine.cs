using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Engine for quiz game logic. Pure functions for state transitions.
/// </summary>
public interface IQuizGameEngine
{
    /// <summary>
    /// Initializes a new quiz game state for a room.
    /// </summary>
    /// <param name="room">The room starting the game.</param>
    /// <param name="locale">Locale for questions (e.g., "nl-BE").</param>
    /// <param name="totalQuestions">Number of questions in the game.</param>
    /// <returns>Initial quiz game state.</returns>
    QuizGameState InitializeGame(Room room, string locale, int totalQuestions = 10);

    /// <summary>
    /// Starts a new question round.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="questionDurationSeconds">Duration for the answering phase.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state with new question, or null if no more questions available.</returns>
    QuizGameState? StartNewQuestion(QuizGameState state, int questionDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions from Question to Answering phase.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="answeringDurationSeconds">Duration for answering.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state in Answering phase.</returns>
    QuizGameState StartAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Records a player's answer. Idempotent - first answer wins.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="playerId">Player submitting the answer.</param>
    /// <param name="optionKey">Selected option key (A, B, C, D).</param>
    /// <returns>Updated state with the answer recorded, or same state if already answered.</returns>
    QuizGameState SubmitAnswer(QuizGameState state, Guid playerId, string optionKey);

    /// <summary>
    /// Transitions to Reveal phase and calculates scores.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="revealDurationSeconds">Duration to show the reveal.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state with correct answer revealed and scores calculated.</returns>
    QuizGameState RevealAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions to Scoreboard phase.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="scoreboardDurationSeconds">Duration to show scoreboard.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state in Scoreboard phase.</returns>
    QuizGameState ShowScoreboard(QuizGameState state, int scoreboardDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Marks the game as finished.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>Updated state in Finished phase.</returns>
    QuizGameState FinishGame(QuizGameState state);

    /// <summary>
    /// Checks if all players have answered.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="playerIds">IDs of active players.</param>
    /// <returns>True if all players have answered.</returns>
    bool AllPlayersAnswered(QuizGameState state, IEnumerable<Guid> playerIds);

    /// <summary>
    /// Checks if there are more questions available.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>True if more questions can be asked.</returns>
    bool HasMoreQuestions(QuizGameState state);

    /// <summary>
    /// Validates if an option key is valid for the current question.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="optionKey">Option key to validate.</param>
    /// <returns>True if the option key is valid.</returns>
    bool IsValidOptionKey(QuizGameState state, string optionKey);
}
