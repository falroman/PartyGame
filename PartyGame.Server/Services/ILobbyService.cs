using PartyGame.Server.DTOs;

namespace PartyGame.Server.Services;

/// <summary>
/// Service for managing lobby operations (host registration, player join/leave).
/// Contains business logic that should not live in controllers or hubs.
/// </summary>
public interface ILobbyService
{
    /// <summary>
    /// Registers a host connection for a room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="connectionId">The host's SignalR connection ID.</param>
    /// <returns>Success if registration succeeded, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> RegisterHostAsync(string roomCode, string connectionId);

    /// <summary>
    /// Handles a player joining a room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's unique ID.</param>
    /// <param name="displayName">The player's display name.</param>
    /// <param name="connectionId">The player's SignalR connection ID.</param>
    /// <returns>Success if join succeeded, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> JoinRoomAsync(string roomCode, Guid playerId, string displayName, string connectionId);

    /// <summary>
    /// Handles a player leaving a room voluntarily.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's unique ID.</param>
    Task LeaveRoomAsync(string roomCode, Guid playerId);

    /// <summary>
    /// Handles a SignalR disconnection.
    /// </summary>
    /// <param name="connectionId">The disconnected connection ID.</param>
    Task HandleDisconnectAsync(string connectionId);

    /// <summary>
    /// Gets the current state of a room as a DTO.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <returns>The room state DTO, or null if room doesn't exist.</returns>
    RoomStateDto? GetRoomState(string roomCode);
}
