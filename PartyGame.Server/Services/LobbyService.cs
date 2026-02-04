using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Server.DTOs;
using PartyGame.Server.Hubs;

namespace PartyGame.Server.Services;

/// <summary>
/// Service for managing lobby operations.
/// Contains business logic for host registration, player join/leave, and disconnects.
/// </summary>
public class LobbyService : ILobbyService
{
    private readonly IRoomStore _roomStore;
    private readonly IConnectionIndex _connectionIndex;
    private readonly IClock _clock;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(
        IRoomStore roomStore,
        IConnectionIndex connectionIndex,
        IClock clock,
        IHubContext<GameHub> hubContext,
        ILogger<LobbyService> logger)
    {
        _roomStore = roomStore;
        _connectionIndex = connectionIndex;
        _clock = clock;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> RegisterHostAsync(string roomCode, string connectionId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("RegisterHost failed: Room {RoomCode} not found", normalizedCode);
            return (false, new ErrorDto("ROOM_NOT_FOUND", $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if this connection is already host of another room
        if (_connectionIndex.TryGet(connectionId, out var existingBinding) && existingBinding != null)
        {
            if (existingBinding.Role == ClientRole.Host && existingBinding.RoomCode != normalizedCode)
            {
                _logger.LogWarning("RegisterHost failed: Connection {ConnectionId} is already host of room {ExistingRoom}",
                    connectionId, existingBinding.RoomCode);
                return (false, new ErrorDto("ALREADY_HOST", "This connection is already hosting another room."));
            }
        }

        // Update room with host connection
        room.HostConnectionId = connectionId;
        _roomStore.Update(room);

        // Bind connection to room
        _connectionIndex.BindHost(connectionId, normalizedCode);

        _logger.LogInformation("Host registered for room {RoomCode} with connection {ConnectionId}",
            normalizedCode, connectionId);

        // Broadcast lobby update to room group
        var roomState = GetRoomState(normalizedCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> JoinRoomAsync(string roomCode, Guid playerId, string displayName, string connectionId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        // Validate display name
        var trimmedName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return (false, new ErrorDto("NAME_INVALID", "Display name cannot be empty."));
        }
        if (trimmedName.Length > 20)
        {
            return (false, new ErrorDto("NAME_INVALID", "Display name cannot exceed 20 characters."));
        }

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} not found", normalizedCode);
            return (false, new ErrorDto("ROOM_NOT_FOUND", $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if room is locked
        if (room.IsLocked)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} is locked", normalizedCode);
            return (false, new ErrorDto("ROOM_LOCKED", "This room is locked and not accepting new players."));
        }

        // Check if room is full (only for new players, not reconnects)
        var isReconnect = room.Players.ContainsKey(playerId);
        if (!isReconnect && room.Players.Count >= room.MaxPlayers)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} is full ({Count}/{Max})",
                normalizedCode, room.Players.Count, room.MaxPlayers);
            return (false, new ErrorDto("ROOM_FULL", $"This room is full ({room.MaxPlayers} players maximum)."));
        }

        // Check for duplicate names (excluding reconnecting player)
        var nameTaken = room.Players.Values
            .Any(p => p.PlayerId != playerId &&
                      p.DisplayName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        if (nameTaken)
        {
            return (false, new ErrorDto("NAME_TAKEN", "This name is already taken by another player."));
        }

        // Add or update player
        if (isReconnect)
        {
            // Reconnect: update connection info
            var player = room.Players[playerId];
            player.ConnectionId = connectionId;
            player.IsConnected = true;
            player.LastSeenUtc = _clock.UtcNow;
            player.DisplayName = trimmedName; // Allow name change on reconnect

            _logger.LogInformation("Player {PlayerId} reconnected to room {RoomCode}", playerId, normalizedCode);
        }
        else
        {
            // New player
            var player = new Player
            {
                PlayerId = playerId,
                DisplayName = trimmedName,
                ConnectionId = connectionId,
                IsConnected = true,
                LastSeenUtc = _clock.UtcNow,
                Score = 0
            };
            room.Players[playerId] = player;

            _logger.LogInformation("Player {PlayerId} ({DisplayName}) joined room {RoomCode}",
                playerId, trimmedName, normalizedCode);
        }

        _roomStore.Update(room);

        // Bind connection
        _connectionIndex.BindPlayer(connectionId, normalizedCode, playerId);

        // Broadcast lobby update
        var roomState = GetRoomState(normalizedCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task LeaveRoomAsync(string roomCode, Guid playerId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return;
        }

        if (room.Players.TryGetValue(playerId, out var player))
        {
            // Unbind connection if it exists
            if (!string.IsNullOrEmpty(player.ConnectionId))
            {
                _connectionIndex.Unbind(player.ConnectionId);
            }

            // Remove player from room
            room.Players.Remove(playerId);
            _roomStore.Update(room);

            _logger.LogInformation("Player {PlayerId} left room {RoomCode}", playerId, normalizedCode);

            // Broadcast lobby update
            var roomState = GetRoomState(normalizedCode);
            if (roomState != null)
            {
                await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
            }
        }
    }

    /// <inheritdoc />
    public async Task HandleDisconnectAsync(string connectionId)
    {
        if (!_connectionIndex.TryGet(connectionId, out var binding) || binding == null)
        {
            return;
        }

        var roomCode = binding.RoomCode;

        if (!_roomStore.TryGetRoom(roomCode, out var room) || room == null)
        {
            _connectionIndex.Unbind(connectionId);
            return;
        }

        if (binding.Role == ClientRole.Host)
        {
            // Host disconnected - clear host connection but keep room
            if (room.HostConnectionId == connectionId)
            {
                room.HostConnectionId = null;
                _roomStore.Update(room);
                _logger.LogInformation("Host disconnected from room {RoomCode}", roomCode);
            }
        }
        else if (binding.Role == ClientRole.Player && binding.PlayerId.HasValue)
        {
            // Player disconnected - mark as disconnected but keep in room
            if (room.Players.TryGetValue(binding.PlayerId.Value, out var player))
            {
                player.IsConnected = false;
                player.ConnectionId = null;
                player.LastSeenUtc = _clock.UtcNow;
                _roomStore.Update(room);

                _logger.LogInformation("Player {PlayerId} disconnected from room {RoomCode}",
                    binding.PlayerId.Value, roomCode);
            }
        }

        _connectionIndex.Unbind(connectionId);

        // Broadcast lobby update
        var roomState = GetRoomState(roomCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{roomCode}").SendAsync("LobbyUpdated", roomState);
        }
    }

    /// <inheritdoc />
    public RoomStateDto? GetRoomState(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return null;
        }

        var players = room.Players.Values
            .Select(p => new PlayerDto(p.PlayerId, p.DisplayName, p.IsConnected, p.Score))
            .OrderBy(p => p.DisplayName)
            .ToList();

        return new RoomStateDto(room.Code, room.Status, room.IsLocked, players);
    }
}
