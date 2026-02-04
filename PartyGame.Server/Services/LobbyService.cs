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
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if this connection is already host of another room
        if (_connectionIndex.TryGet(connectionId, out var existingBinding) && existingBinding != null)
        {
            if (existingBinding.Role == ClientRole.Host && existingBinding.RoomCode != normalizedCode)
            {
                _logger.LogWarning("RegisterHost failed: Connection {ConnectionId} is already host of room {ExistingRoom}",
                    connectionId, existingBinding.RoomCode);
                return (false, new ErrorDto(ErrorCodes.AlreadyHost, "This connection is already hosting another room."));
            }
        }

        // Update room with host connection
        room.HostConnectionId = connectionId;
        room.HostDisconnectedAtUtc = null; // Clear disconnect timestamp when host connects
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
            return (false, new ErrorDto(ErrorCodes.NameInvalid, "Display name cannot be empty."));
        }
        if (trimmedName.Length > 20)
        {
            return (false, new ErrorDto(ErrorCodes.NameInvalid, "Display name cannot exceed 20 characters."));
        }

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} not found", normalizedCode);
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if this is a reconnect (existing player)
        var isReconnect = room.Players.ContainsKey(playerId);

        // Check if room is locked (only block new players, not reconnects)
        if (room.IsLocked && !isReconnect)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} is locked", normalizedCode);
            return (false, new ErrorDto(ErrorCodes.RoomLocked, "This room is locked and not accepting new players."));
        }

        // Check if room is full (only for new players, not reconnects)
        if (!isReconnect && room.Players.Count >= room.MaxPlayers)
        {
            _logger.LogWarning("JoinRoom failed: Room {RoomCode} is full ({Count}/{Max})",
                normalizedCode, room.Players.Count, room.MaxPlayers);
            return (false, new ErrorDto(ErrorCodes.RoomFull, $"This room is full ({room.MaxPlayers} players maximum)."));
        }

        // Check for duplicate names (excluding reconnecting player)
        var nameTaken = room.Players.Values
            .Any(p => p.PlayerId != playerId &&
                      p.DisplayName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        if (nameTaken)
        {
            return (false, new ErrorDto(ErrorCodes.NameTaken, "This name is already taken by another player."));
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
            // Host disconnected - clear host connection and track disconnect time
            if (room.HostConnectionId == connectionId)
            {
                room.HostConnectionId = null;
                room.HostDisconnectedAtUtc = _clock.UtcNow;
                _roomStore.Update(room);
                _logger.LogInformation("Host disconnected from room {RoomCode} at {DisconnectTime}", 
                    roomCode, room.HostDisconnectedAtUtc);
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

                _logger.LogInformation("Player {PlayerId} disconnected from room {RoomCode} at {DisconnectTime}",
                    binding.PlayerId.Value, roomCode, player.LastSeenUtc);
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
    public async Task<(bool Success, ErrorDto? Error)> SetRoomLockedAsync(string roomCode, string connectionId, bool isLocked)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("SetRoomLocked failed: Room {RoomCode} not found", normalizedCode);
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if caller is the host
        if (!IsHostOfRoom(normalizedCode, connectionId))
        {
            _logger.LogWarning("SetRoomLocked failed: Connection {ConnectionId} is not host of room {RoomCode}", 
                connectionId, normalizedCode);
            return (false, new ErrorDto(ErrorCodes.NotHost, "Only the host can lock or unlock the room."));
        }

        // Update room locked state
        room.IsLocked = isLocked;
        _roomStore.Update(room);

        _logger.LogInformation("Room {RoomCode} lock state changed to {IsLocked} by host", 
            normalizedCode, isLocked);

        // Broadcast lobby update
        var roomState = GetRoomState(normalizedCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
        }

        return (true, null);
    }

    /// <inheritdoc />
    public bool IsHostOfRoom(string roomCode, string connectionId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return false;
        }

        return room.HostConnectionId == connectionId;
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

        // Map CurrentGame to GameSessionDto if present
        GameSessionDto? currentGameDto = null;
        if (room.CurrentGame != null)
        {
            currentGameDto = new GameSessionDto(
                room.CurrentGame.GameType.ToString(),
                room.CurrentGame.Phase,
                room.CurrentGame.StartedUtc
            );
        }

        return new RoomStateDto(room.Code, room.Status, room.IsLocked, players, currentGameDto);
    }

    /// <inheritdoc />
    public async Task<int> RemoveDisconnectedPlayersAsync(string roomCode, TimeSpan gracePeriod)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var playersToRemove = room.Players.Values
            .Where(p => !p.IsConnected && (now - p.LastSeenUtc) > gracePeriod)
            .Select(p => p.PlayerId)
            .ToList();

        if (playersToRemove.Count == 0)
        {
            return 0;
        }

        foreach (var playerId in playersToRemove)
        {
            room.Players.Remove(playerId);
        }

        _roomStore.Update(room);

        _logger.LogInformation("Removed {Count} disconnected player(s) from room {RoomCode}: {PlayerIds}",
            playersToRemove.Count, normalizedCode, string.Join(", ", playersToRemove));

        // Broadcast lobby update
        var roomState = GetRoomState(normalizedCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
        }

        return playersToRemove.Count;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetHostlessRoomsForCleanup(TimeSpan ttl)
    {
        var now = _clock.UtcNow;
        var roomsToRemove = new List<string>();

        foreach (var room in _roomStore.GetAll())
        {
            // Room has no host connection
            if (room.HostConnectionId == null)
            {
                // Check if host has been disconnected longer than TTL
                if (room.HostDisconnectedAtUtc.HasValue)
                {
                    if ((now - room.HostDisconnectedAtUtc.Value) > ttl)
                    {
                        roomsToRemove.Add(room.Code);
                    }
                }
                else
                {
                    // Host never registered - check room creation time
                    if ((now - room.CreatedUtc) > ttl)
                    {
                        roomsToRemove.Add(room.Code);
                    }
                }
            }
        }

        return roomsToRemove;
    }

    /// <inheritdoc />
    public void RemoveRoom(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        _roomStore.Remove(normalizedCode);
        _logger.LogInformation("Room {RoomCode} removed by cleanup", normalizedCode);
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> StartGameAsync(string roomCode, string connectionId, GameType gameType)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("StartGame failed: Room {RoomCode} not found", normalizedCode);
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, $"Room with code '{normalizedCode}' does not exist."));
        }

        // Check if caller is the host
        if (!IsHostOfRoom(normalizedCode, connectionId))
        {
            _logger.LogWarning("StartGame failed: Connection {ConnectionId} is not host of room {RoomCode}", 
                connectionId, normalizedCode);
            return (false, new ErrorDto(ErrorCodes.NotHost, "Only the host can start the game."));
        }

        // Check if room is in Lobby state
        if (room.Status != RoomStatus.Lobby)
        {
            _logger.LogWarning("StartGame failed: Room {RoomCode} is not in Lobby state (current: {Status})", 
                normalizedCode, room.Status);
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Game cannot be started. The room is not in lobby state."));
        }

        // Check if there are at least 2 players
        if (room.Players.Count < 2)
        {
            _logger.LogWarning("StartGame failed: Room {RoomCode} has only {Count} player(s), need at least 2", 
                normalizedCode, room.Players.Count);
            return (false, new ErrorDto(ErrorCodes.NotEnoughPlayers, "At least 2 players are required to start the game."));
        }

        // Create game session
        var gameSession = new GameSession
        {
            GameType = gameType,
            Phase = "Starting",
            StartedUtc = _clock.UtcNow,
            State = null
        };

        // Update room state
        room.Status = RoomStatus.InGame;
        room.IsLocked = true;
        room.CurrentGame = gameSession;
        _roomStore.Update(room);

        _logger.LogInformation("Game started in room {RoomCode}: Type={GameType}, Phase={Phase}", 
            normalizedCode, gameType, gameSession.Phase);

        // Broadcast lobby update (includes new game state)
        var roomState = GetRoomState(normalizedCode);
        if (roomState != null)
        {
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", roomState);
            
            // Also send GameStarted event for clients that want to handle it separately
            var gameSessionDto = new GameSessionDto(
                gameSession.GameType.ToString(),
                gameSession.Phase,
                gameSession.StartedUtc
            );
            await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("GameStarted", gameSessionDto);
        }

        return (true, null);
    }
}
