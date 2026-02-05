using PartyGame.Core.Enums;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Dictionary;
using PartyGame.Core.Models.Ranking;
using PartyGame.Core.Models.Quiz;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Engine for quiz game logic. Pure functions for state transitions.
/// </summary>
public interface IQuizGameEngine
{
    /// <summary>
    /// Initializes a new quiz game state for a room with planned rounds.
    /// </summary>
    /// <param name="room">The room starting the game.</param>
    /// <param name="locale">Locale for questions (e.g., "nl-BE").</param>
    /// <param name="plannedRounds">Sequence of round types. DictionaryGame must be last.</param>
    /// <returns>Initial quiz game state.</returns>
    QuizGameState InitializeGame(Room room, string locale, IEnumerable<RoundType> plannedRounds);

    /// <summary>
    /// Initializes a new quiz game state for a room (legacy, uses default round plan).
    /// </summary>
    QuizGameState InitializeGame(Room room, string locale, int totalQuestions = 10);

    /// <summary>
    /// Gets the next round type from the planned rounds.
    /// </summary>
    RoundType? GetNextRoundType(QuizGameState state);

    /// <summary>
    /// Checks if there are more planned rounds.
    /// </summary>
    bool HasMorePlannedRounds(QuizGameState state);

    /// <summary>
    /// Starts a new round with category selection phase.
    /// </summary>
    QuizGameState StartNewRound(QuizGameState state, int categorySelectionDurationSeconds, DateTime currentTime);

    #region Ranking Stars Methods

    /// <summary>
    /// Starts a Ranking Stars round.
    /// </summary>
    QuizGameState StartRankingRound(QuizGameState state, int promptDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Starts a new ranking prompt within the ranking round.
    /// </summary>
    QuizGameState StartRankingPrompt(QuizGameState state, RankingPrompt prompt, int promptDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions to ranking voting phase.
    /// </summary>
    QuizGameState StartRankingVotingPhase(QuizGameState state, int votingDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Records a player's vote. Idempotent - first vote wins.
    /// </summary>
    QuizGameState SubmitRankingVote(QuizGameState state, Guid voterPlayerId, Guid votedForPlayerId, DateTime voteTime);

    /// <summary>
    /// Reveals the ranking votes and calculates scores.
    /// </summary>
    QuizGameState RevealRankingVotes(QuizGameState state, int revealDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Checks if there are more prompts in the ranking round.
    /// </summary>
    bool HasMoreRankingPrompts(QuizGameState state);

    /// <summary>
    /// Checks if all players have voted.
    /// </summary>
    bool AllRankingVoted(QuizGameState state, IEnumerable<Guid> playerIds);

    /// <summary>
    /// Validates if a vote is valid (voter and target exist, not self-vote).
    /// </summary>
    bool IsValidRankingVote(QuizGameState state, Guid voterPlayerId, Guid votedForPlayerId);

    /// <summary>
    /// Calculates the ranking vote result (winners, vote counts, etc.).
    /// </summary>
    RankingVoteResult CalculateRankingResult(QuizGameState state);

    #endregion

    #region Dictionary Game Methods

    /// <summary>
    /// Starts the Dictionary Game round (final round, mandatory).
    /// </summary>
    QuizGameState StartDictionaryRound(QuizGameState state, int wordDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Starts a new dictionary word within the dictionary round.
    /// </summary>
    QuizGameState StartDictionaryWord(QuizGameState state, DictionaryQuestion question, int wordDisplayDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions to dictionary answering phase (show options).
    /// </summary>
    QuizGameState StartDictionaryAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Records a player's dictionary answer. Idempotent - first answer wins.
    /// </summary>
    QuizGameState SubmitDictionaryAnswer(QuizGameState state, Guid playerId, int optionIndex, DateTime answerTime);

    /// <summary>
    /// Reveals the dictionary answer and calculates scores with speed bonus.
    /// </summary>
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

    #endregion

    #region Category Quiz Methods

    /// <summary>
    /// Selects the round leader based on current scores.
    /// </summary>
    Guid SelectRoundLeader(QuizGameState state);

    /// <summary>
    /// Sets the category for the current round.
    /// </summary>
    QuizGameState SetRoundCategory(QuizGameState state, string category);

    /// <summary>
    /// Starts a new question within the current round.
    /// </summary>
    QuizGameState? StartNewQuestion(QuizGameState state, int questionDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions from Question to Answering phase.
    /// </summary>
    QuizGameState StartAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Records a player's answer. Idempotent - first answer wins.
    /// </summary>
    QuizGameState SubmitAnswer(QuizGameState state, Guid playerId, string optionKey);

    /// <summary>
    /// Transitions to Reveal phase and calculates scores.
    /// </summary>
    QuizGameState RevealAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Transitions to Scoreboard phase.
    /// </summary>
    QuizGameState ShowScoreboard(QuizGameState state, int scoreboardDurationSeconds, DateTime currentTime);

    /// <summary>
    /// Marks the game as finished.
    /// </summary>
    QuizGameState FinishGame(QuizGameState state);

    /// <summary>
    /// Checks if all players have answered.
    /// </summary>
    bool AllPlayersAnswered(QuizGameState state, IEnumerable<Guid> playerIds);

    /// <summary>
    /// Checks if there are more questions available in the current round.
    /// </summary>
    bool HasMoreQuestionsInRound(QuizGameState state);

    /// <summary>
    /// Checks if there are more questions available in the game.
    /// </summary>
    bool HasMoreQuestions(QuizGameState state);

    /// <summary>
    /// Checks if the dictionary round should start (deprecated - use GetNextRoundType).
    /// </summary>
    bool ShouldStartDictionaryRound(QuizGameState state);

    /// <summary>
    /// Validates if an option key is valid for the current question.
    /// </summary>
    bool IsValidOptionKey(QuizGameState state, string optionKey);

    /// <summary>
    /// Checks if a category is valid for selection.
    /// </summary>
    bool IsValidCategory(QuizGameState state, string category);

    #endregion
}
