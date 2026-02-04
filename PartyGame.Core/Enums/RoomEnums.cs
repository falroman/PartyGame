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
