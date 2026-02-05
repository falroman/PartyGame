using PartyGame.Core.Models;
using PartyGame.Core.Models.Dictionary;
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
    /// Starts a new round with category selection phase.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="categorySelectionDurationSeconds">Duration for category selection.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state in CategorySelection phase.</returns>
    QuizGameState StartNewRound(QuizGameState state, int categorySelectionDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Starts the Dictionary Game round (final round, mandatory).
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="wordDisplayDurationSeconds">Duration to show word before options.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state with DictionaryGame round started.</returns>
    QuizGameState StartDictionaryRound(QuizGameState state, int wordDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Starts a new dictionary word within the dictionary round.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="question">The dictionary question to display.</param>
    /// <param name="wordDisplayDurationSeconds">Duration to show word before options.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state with new dictionary word.</returns>
    QuizGameState StartDictionaryWord(QuizGameState state, DictionaryQuestion question, int wordDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions to dictionary answering phase (show options).
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="answeringDurationSeconds">Duration for answering.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state in DictionaryAnswering phase.</returns>
    QuizGameState StartDictionaryAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Records a player's dictionary answer. Idempotent - first answer wins.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="playerId">Player submitting the answer.</param>
    /// <param name="optionIndex">Selected option index (0-3).</param>
    /// <param name="answerTime">Time when answer was submitted.</param>
    /// <returns>Updated state with the answer recorded.</returns>
    QuizGameState SubmitDictionaryAnswer(QuizGameState state, Guid playerId, int optionIndex, DateTime answerTime);

    /// <summary>
    /// Reveals the dictionary answer and calculates scores with speed bonus.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="revealDurationSeconds">Duration to show the reveal.</param>
    /// <param name="currentTime">Current UTC time.</param>
    /// <returns>Updated state with scores calculated.</returns>
    QuizGameState RevealDictionaryAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Checks if there are more words in the dictionary round.
    /// </summary>
    bool HasMoreDictionaryWords(QuizGameState state);

    /// <summary>
    /// Checks if all players have answered the dictionary question.
    /// </summary>
    bool AllDictionaryAnswered(QuizGameState state, IEnumerable<Guid> playerIds);

    /// <summary>
    /// Validates if an option index is valid for dictionary answer (0-3).
    /// </summary>
    bool IsValidDictionaryOption(int optionIndex);

    /// <summary>
    /// Selects the round leader based on current scores.
    /// Rule: Player with lowest score chooses (fairness), ties broken by player order.
    /// Same player cannot choose twice in a row.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>PlayerId of the selected round leader.</returns>
    Guid SelectRoundLeader(QuizGameState state);

    /// <summary>
    /// Sets the category for the current round and transitions to Question phase.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="category">Selected category.</param>
    /// <returns>Updated state with category set.</returns>
    QuizGameState SetRoundCategory(QuizGameState state, string category);

    /// <summary>
    /// Starts a new question within the current round.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="questionDurationSeconds">Duration for the question display phase.</param>
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
    /// Checks if there are more questions available in the current round.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>True if more questions can be asked in the current round.</returns>
    bool HasMoreQuestionsInRound(QuizGameState state);

    /// <summary>
    /// Checks if there are more questions available in the game (before dictionary round).
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <returns>True if more questions can be asked.</returns>
    bool HasMoreQuestions(QuizGameState state);

    /// <summary>
    /// Checks if the dictionary round should start (all quiz rounds complete).
    /// </summary>
    bool ShouldStartDictionaryRound(QuizGameState state);

    /// <summary>
    /// Validates if an option key is valid for the current question.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="optionKey">Option key to validate.</param>
    /// <returns>True if the option key is valid.</returns>
    bool IsValidOptionKey(QuizGameState state, string optionKey);

    /// <summary>
    /// Checks if a category is valid for selection.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="category">Category to validate.</param>
    /// <returns>True if the category can be selected.</returns>
    bool IsValidCategory(QuizGameState state, string category);
}
