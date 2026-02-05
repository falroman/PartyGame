namespace PartyGame.Core.Enums;

/// <summary>
/// Represents the current status of a game room.
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// Room is in lobby phase, waiting for players and game to start.
    /// </summary>
    Lobby,

    /// <summary>
    /// A game is currently in progress.
    /// </summary>
    InGame,

    /// <summary>
    /// The game has ended.
    /// </summary>
    Finished
}

/// <summary>
/// Represents the role of a connected client.
/// </summary>
public enum ClientRole
{
    /// <summary>
    /// The client is the host (TV view).
    /// </summary>
    Host,

    /// <summary>
    /// The client is a player (phone controller).
    /// </summary>
    Player
}

/// <summary>
/// Represents the type of game being played.
/// </summary>
public enum GameType
{
    /// <summary>
    /// Quiz game type.
    /// </summary>
    Quiz
}

/// <summary>
/// Represents the type of a game round.
/// </summary>
public enum RoundType
{
    /// <summary>
    /// A round with questions from a single category, chosen by a player.
    /// </summary>
    CategoryQuiz,

    /// <summary>
    /// A round where players vote for another player as the answer to fun prompts.
    /// </summary>
    RankingStars,

    /// <summary>
    /// A round with dictionary words - players guess the correct definition.
    /// This is always the final round and serves as a catch-up mechanism.
    /// </summary>
    DictionaryGame
}

/// <summary>
/// Represents the phases of a quiz game.
/// </summary>
public enum QuizPhase
{
    /// <summary>
    /// Waiting for the round leader to select a category.
    /// </summary>
    CategorySelection,

    /// <summary>
    /// Displaying the question to players (pre-answer phase).
    /// </summary>
    Question,

    /// <summary>
    /// Players can submit their answers.
    /// </summary>
    Answering,

    /// <summary>
    /// Revealing the correct answer.
    /// </summary>
    Reveal,

    /// <summary>
    /// Showing the scoreboard between questions or at game end.
    /// </summary>
    Scoreboard,

    /// <summary>
    /// Game has finished.
    /// </summary>
    Finished,

    /// <summary>
    /// Dictionary game: showing the word (suspense phase before options appear).
    /// </summary>
    DictionaryWord,

    /// <summary>
    /// Dictionary game: showing word + 4 definition options.
    /// </summary>
    DictionaryAnswering,

    /// <summary>
    /// Ranking Stars: showing the prompt (intro phase).
    /// </summary>
    RankingPrompt,

    /// <summary>
    /// Ranking Stars: players vote for another player.
    /// </summary>
    RankingVoting,

    /// <summary>
    /// Ranking Stars: revealing the votes and winner.
    /// </summary>
    RankingReveal
}
