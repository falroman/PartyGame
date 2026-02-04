using PartyGame.Core.Enums;

namespace PartyGame.Core.Models;

/// <summary>
/// Represents a game room where players gather to play.
/// </summary>
public class Room
{
    /// <summary>
    /// Unique 4-character room code (e.g., "A1B2").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// When the room was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Current status of the room.
    /// </summary>
    public RoomStatus Status { get; set; } = RoomStatus.Lobby;

    /// <summary>
    /// Whether the room is locked (no new players can join).
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// SignalR connection ID of the host. Null if host hasn't registered yet.
    /// </summary>
    public string? HostConnectionId { get; set; }

    /// <summary>
    /// Players in the room, keyed by PlayerId.
    /// </summary>
    public Dictionary<Guid, Player> Players { get; set; } = new();

    /// <summary>
    /// Maximum number of players allowed in the room.
    /// </summary>
    public int MaxPlayers { get; set; } = 8;
}
