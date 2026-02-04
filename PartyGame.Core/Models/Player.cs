namespace PartyGame.Core.Models;

/// <summary>
/// Represents a player in a game room.
/// </summary>
public class Player
{
    /// <summary>
    /// Unique identifier for the player. Stored in localStorage for reconnection.
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Display name shown to other players.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Current SignalR connection ID. Null if disconnected.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Timestamp of last activity (for idle detection).
    /// </summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Whether the player currently has an active connection.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Player's current score in the game.
    /// </summary>
    public int Score { get; set; }
}
