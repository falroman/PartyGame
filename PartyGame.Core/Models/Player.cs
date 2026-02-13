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

    /// <summary>
    /// Indicates if the player is a server-side bot.
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// Bot skill level from 0 to 100 (higher = more accurate).
    /// </summary>
    public int BotSkill { get; set; }

    /// <summary>
    /// Avatar preset ID (e.g., "jelly_01"). Null for uploaded avatars.
    /// </summary>
    public string? AvatarPresetId { get; set; }

    /// <summary>
    /// Avatar URL for uploaded avatars. Null for preset avatars.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Type of avatar being used.
    /// </summary>
    public AvatarKind AvatarKind { get; set; } = AvatarKind.Preset;
}

/// <summary>
/// Type of avatar
/// </summary>
public enum AvatarKind
{
    Preset = 0,
    Uploaded = 1
}
