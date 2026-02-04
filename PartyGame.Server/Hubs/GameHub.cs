using Microsoft.AspNetCore.SignalR;
using PartyGame.Core.Enums;
using PartyGame.Server.DTOs;
using PartyGame.Server.Services;

namespace PartyGame.Server.Hubs;

/// <summary>
/// Main SignalR hub for realtime game communication.
/// Handles host registration, player joins, lobby updates, and game events.
/// </summary>
public class GameHub : Hub
{
    private readonly ILobbyService _lobbyService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(ILobbyService lobbyService, ILogger<GameHub> logger)
    {
        _lobbyService = lobbyService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        
        await _lobbyService.HandleDisconnectAsync(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Host registers themselves for a room after creating it via REST API.
    /// </summary>
    public async Task RegisterHost(string roomCode)
    {
        _logger.LogInformation("RegisterHost called for room {RoomCode} by {ConnectionId}", 
            roomCode, Context.ConnectionId);

        var (success, error) = await _lobbyService.RegisterHostAsync(roomCode, Context.ConnectionId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Add host to room group for receiving broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");

        // Send current room state to host
        var roomState = _lobbyService.GetRoomState(roomCode);
        if (roomState != null)
        {
            await Clients.Caller.SendAsync("LobbyUpdated", roomState);
        }
    }

    /// <summary>
    /// Player joins a room with their playerId and display name.
    /// </summary>
    public async Task JoinRoom(string roomCode, Guid playerId, string displayName)
    {
        _logger.LogInformation("JoinRoom called for room {RoomCode} by player {PlayerId} ({DisplayName})", 
            roomCode, playerId, displayName);

        var (success, error) = await _lobbyService.JoinRoomAsync(roomCode, playerId, displayName, Context.ConnectionId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Add player to room group for receiving broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");

        // Send current room state to player
        var roomState = _lobbyService.GetRoomState(roomCode);
        if (roomState != null)
        {
            await Clients.Caller.SendAsync("LobbyUpdated", roomState);
        }
    }

    /// <summary>
    /// Player leaves a room voluntarily.
    /// </summary>
    public async Task LeaveRoom(string roomCode, Guid playerId)
    {
        _logger.LogInformation("LeaveRoom called for room {RoomCode} by player {PlayerId}", 
            roomCode, playerId);

        await _lobbyService.LeaveRoomAsync(roomCode, playerId);

        // Remove from room group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");
    }

    /// <summary>
    /// Host sets the locked state of a room.
    /// Only the host can lock or unlock a room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="isLocked">True to lock the room, false to unlock.</param>
    public async Task SetRoomLocked(string roomCode, bool isLocked)
    {
        _logger.LogInformation("SetRoomLocked called for room {RoomCode} with isLocked={IsLocked} by {ConnectionId}", 
            roomCode, isLocked, Context.ConnectionId);

        var (success, error) = await _lobbyService.SetRoomLockedAsync(roomCode, Context.ConnectionId, isLocked);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Host starts the game. Only the host can start a game, and at least 2 players are required.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="gameType">The type of game to start (e.g., "Quiz").</param>
    public async Task StartGame(string roomCode, string gameType)
    {
        _logger.LogInformation("StartGame called for room {RoomCode} with gameType={GameType} by {ConnectionId}", 
            roomCode, gameType, Context.ConnectionId);

        // Parse game type from string
        if (!Enum.TryParse<GameType>(gameType, ignoreCase: true, out var parsedGameType))
        {
            _logger.LogWarning("StartGame failed: Invalid game type '{GameType}'", gameType);
            await Clients.Caller.SendAsync("Error", new ErrorDto(ErrorCodes.InvalidState, $"Invalid game type: {gameType}"));
            return;
        }

        var (success, error) = await _lobbyService.StartGameAsync(roomCode, Context.ConnectionId, parsedGameType);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }
}
