using PartyGame.Core.Enums;
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
    /// <param name="avatarPresetId">Optional avatar preset ID selected by the player.</param>
    /// <returns>Success if join succeeded, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> JoinRoomAsync(string roomCode, Guid playerId, string displayName, string connectionId, string? avatarPresetId = null);

    /// <summary>
    /// Handles a player leaving a room voluntarily.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's unique ID.</param>
    Task LeaveRoomAsync(string roomCode, Guid playerId);

    /// <summary>
    /// Adds server-side bot players to a room without a SignalR connection.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="count">Number of bots to add.</param>
    /// <returns>Success if bots added, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> AddBotPlayersAsync(string roomCode, int count);

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

    /// <summary>
    /// Sets the locked state of a room. Only the host can perform this action.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="connectionId">The connection ID attempting the action.</param>
    /// <param name="isLocked">Whether the room should be locked.</param>
    /// <returns>Success if operation succeeded, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> SetRoomLockedAsync(string roomCode, string connectionId, bool isLocked);

    /// <summary>
    /// Checks if the given connection is the host of the specified room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="connectionId">The connection ID to check.</param>
    /// <returns>True if the connection is the host of the room.</returns>
    bool IsHostOfRoom(string roomCode, string connectionId);

    /// <summary>
    /// Removes disconnected players from a room who have been disconnected longer than the grace period.
    /// Broadcasts LobbyUpdated if any players were removed.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="gracePeriod">The grace period after which disconnected players are removed.</param>
    /// <returns>The number of players removed.</returns>
    Task<int> RemoveDisconnectedPlayersAsync(string roomCode, TimeSpan gracePeriod);

    /// <summary>
    /// Gets all room codes that should be removed because they have no active host
    /// and the host has been disconnected longer than the TTL.
    /// </summary>
    /// <param name="ttl">The time-to-live for hostless rooms.</param>
    /// <returns>List of room codes that should be removed.</returns>
    IReadOnlyList<string> GetHostlessRoomsForCleanup(TimeSpan ttl);

    /// <summary>
    /// Removes a room completely.
    /// </summary>
    /// <param name="roomCode">The room code to remove.</param>
    void RemoveRoom(string roomCode);

    /// <summary>
    /// Starts a game in the specified room. Only the host can perform this action.
    /// Requires at least 2 players in the room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="connectionId">The connection ID attempting the action (must be host).</param>
    /// <param name="gameType">The type of game to start.</param>
    /// <returns>Success if game started, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> StartGameAsync(string roomCode, string connectionId, GameType gameType);

    /// <summary>
    /// Resets the room back to lobby state. Only the host can perform this action.
    /// Keeps all players in the room but clears game state and scores.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="connectionId">The connection ID attempting the action (must be host).</param>
    /// <returns>Success if reset succeeded, error details otherwise.</returns>
    Task<(bool Success, ErrorDto? Error)> ResetToLobbyAsync(string roomCode, string connectionId);
}
