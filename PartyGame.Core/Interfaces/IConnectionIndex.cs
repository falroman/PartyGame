using PartyGame.Core.Enums;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Represents a binding between a SignalR connection and a room/player.
/// </summary>
public class ConnectionBinding
{
    /// <summary>
    /// The SignalR connection ID.
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// The room code this connection is bound to.
    /// </summary>
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>
    /// The role of this connection (Host or Player).
    /// </summary>
    public ClientRole Role { get; set; }

    /// <summary>
    /// The player ID if this is a player connection. Null for hosts.
    /// </summary>
    public Guid? PlayerId { get; set; }
}

/// <summary>
/// Index for tracking SignalR connections and their room/player bindings.
/// Essential for handling disconnects and reconnects.
/// </summary>
public interface IConnectionIndex
{
    /// <summary>
    /// Binds a host connection to a room.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="roomCode">The room code.</param>
    void BindHost(string connectionId, string roomCode);

    /// <summary>
    /// Binds a player connection to a room.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's unique ID.</param>
    void BindPlayer(string connectionId, string roomCode, Guid playerId);

    /// <summary>
    /// Attempts to get the binding for a connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="binding">The binding if found.</param>
    /// <returns>True if binding exists, false otherwise.</returns>
    bool TryGet(string connectionId, out ConnectionBinding? binding);

    /// <summary>
    /// Removes the binding for a connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    void Unbind(string connectionId);

    /// <summary>
    /// Gets all bindings for a specific room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <returns>All connection bindings for the room.</returns>
    IEnumerable<ConnectionBinding> GetBindingsForRoom(string roomCode);
}
