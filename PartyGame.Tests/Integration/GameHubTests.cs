using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using PartyGame.Core.Enums;
using PartyGame.Server.DTOs;

namespace PartyGame.Tests.Integration;

/// <summary>
/// Integration tests for SignalR GameHub host registration and lobby functionality.
/// </summary>
public class GameHubTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;
    private HubConnection? _hostConnection;

    public GameHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_hostConnection != null)
        {
            await _hostConnection.DisposeAsync();
        }
    }

    private HubConnection CreateHubConnection()
    {
        var hubUrl = new Uri(_httpClient.BaseAddress!, "/hub/game");
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    [Fact]
    public async Task RegisterHost_WithValidRoom_Succeeds()
    {
        // Arrange - Create a room via REST API
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        RoomStateDto? receivedState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => receivedState = state);

        await _hostConnection.StartAsync();

        // Act
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        // Assert - Give a moment for async processing
        await Task.Delay(100);
        receivedState.Should().NotBeNull();
        receivedState!.RoomCode.Should().Be(roomCode);
        receivedState.Status.Should().Be(RoomStatus.Lobby);
        receivedState.Players.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterHost_WithInvalidRoom_ReturnsError()
    {
        // Arrange
        _hostConnection = CreateHubConnection();
        ErrorDto? receivedError = null;
        _hostConnection.On<ErrorDto>("Error", error => receivedError = error);

        await _hostConnection.StartAsync();

        // Act
        await _hostConnection.InvokeAsync("RegisterHost", "XXXX");

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.RoomNotFound);
    }

    [Fact]
    public async Task JoinRoom_WithValidRoomAndName_Succeeds()
    {
        // Arrange - Create room and register host
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        var playerConnection = CreateHubConnection();

        RoomStateDto? hostReceivedState = null;
        RoomStateDto? playerReceivedState = null;

        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => hostReceivedState = state);
        playerConnection.On<RoomStateDto>("LobbyUpdated", state => playerReceivedState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        await playerConnection.StartAsync();

        // Act
        var playerId = Guid.NewGuid();
        await playerConnection.InvokeAsync("JoinRoom", roomCode, playerId, "TestPlayer");

        // Assert
        await Task.Delay(100);
        
        // Host should receive updated state
        hostReceivedState.Should().NotBeNull();
        hostReceivedState!.Players.Should().HaveCount(1);
        hostReceivedState.Players[0].DisplayName.Should().Be("TestPlayer");
        hostReceivedState.Players[0].IsConnected.Should().BeTrue();

        // Player should also receive state
        playerReceivedState.Should().NotBeNull();
        playerReceivedState!.Players.Should().HaveCount(1);

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_WithInvalidRoom_ReturnsError()
    {
        // Arrange
        var playerConnection = CreateHubConnection();
        ErrorDto? receivedError = null;
        playerConnection.On<ErrorDto>("Error", error => receivedError = error);

        await playerConnection.StartAsync();

        // Act
        await playerConnection.InvokeAsync("JoinRoom", "XXXX", Guid.NewGuid(), "TestPlayer");

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.RoomNotFound);

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_WithEmptyName_ReturnsError()
    {
        // Arrange - Create room
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        var playerConnection = CreateHubConnection();
        ErrorDto? receivedError = null;
        playerConnection.On<ErrorDto>("Error", error => receivedError = error);

        await playerConnection.StartAsync();

        // Act
        await playerConnection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), "");

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.NameInvalid);

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_WithDuplicateName_ReturnsError()
    {
        // Arrange - Create room and add first player
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        var player1Connection = CreateHubConnection();
        var player2Connection = CreateHubConnection();
        ErrorDto? receivedError = null;
        player2Connection.On<ErrorDto>("Error", error => receivedError = error);

        await player1Connection.StartAsync();
        await player1Connection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), "SameName");

        await player2Connection.StartAsync();

        // Act
        await player2Connection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), "SameName");

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.NameTaken);

        await player1Connection.DisposeAsync();
        await player2Connection.DisposeAsync();
    }

    [Fact]
    public async Task LeaveRoom_RemovesPlayerFromLobby()
    {
        // Arrange - Create room and add player
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        var playerConnection = CreateHubConnection();

        RoomStateDto? lastHostState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastHostState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        var playerId = Guid.NewGuid();
        await playerConnection.StartAsync();
        await playerConnection.InvokeAsync("JoinRoom", roomCode, playerId, "LeavingPlayer");
        await Task.Delay(100);

        // Act
        await playerConnection.InvokeAsync("LeaveRoom", roomCode, playerId);

        // Assert
        await Task.Delay(100);
        lastHostState.Should().NotBeNull();
        lastHostState!.Players.Should().BeEmpty();

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task Reconnect_WithSamePlayerId_UpdatesConnection()
    {
        // Arrange - Create room and add player
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        RoomStateDto? lastHostState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastHostState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        var playerId = Guid.NewGuid();
        var player1Connection = CreateHubConnection();
        await player1Connection.StartAsync();
        await player1Connection.InvokeAsync("JoinRoom", roomCode, playerId, "ReconnectPlayer");
        await Task.Delay(100);

        // Simulate disconnect
        await player1Connection.DisposeAsync();
        await Task.Delay(100);

        // Player should be marked as disconnected
        lastHostState!.Players.Should().HaveCount(1);
        lastHostState.Players[0].IsConnected.Should().BeFalse();

        // Act - Reconnect with same playerId
        var player2Connection = CreateHubConnection();
        await player2Connection.StartAsync();
        await player2Connection.InvokeAsync("JoinRoom", roomCode, playerId, "ReconnectPlayer");

        // Assert
        await Task.Delay(100);
        lastHostState.Should().NotBeNull();
        lastHostState!.Players.Should().HaveCount(1);
        lastHostState.Players[0].IsConnected.Should().BeTrue();
        lastHostState.Players[0].PlayerId.Should().Be(playerId);

        await player2Connection.DisposeAsync();
    }

    #region Room Locking Tests

    [Fact]
    public async Task SetRoomLocked_ByHost_LocksRoom()
    {
        // Arrange - Create room and register host
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        RoomStateDto? lastState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);
        await Task.Delay(100);

        // Initial state should be unlocked
        lastState!.IsLocked.Should().BeFalse();

        // Act
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, true);

        // Assert
        await Task.Delay(100);
        lastState.Should().NotBeNull();
        lastState!.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task SetRoomLocked_ByHost_CanUnlockRoom()
    {
        // Arrange - Create room, register host, and lock room
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        RoomStateDto? lastState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, true);
        await Task.Delay(100);

        lastState!.IsLocked.Should().BeTrue();

        // Act
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, false);

        // Assert
        await Task.Delay(100);
        lastState!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task SetRoomLocked_ByNonHost_ReturnsNotHostError()
    {
        // Arrange - Create room and register host
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        var playerConnection = CreateHubConnection();

        ErrorDto? receivedError = null;
        RoomStateDto? lastState = null;

        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastState = state);
        playerConnection.On<ErrorDto>("Error", error => receivedError = error);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        var playerId = Guid.NewGuid();
        await playerConnection.StartAsync();
        await playerConnection.InvokeAsync("JoinRoom", roomCode, playerId, "TestPlayer");
        await Task.Delay(100);

        // Act - Player tries to lock room
        await playerConnection.InvokeAsync("SetRoomLocked", roomCode, true);

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.NotHost);
        lastState!.IsLocked.Should().BeFalse(); // Room should still be unlocked

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_WhenRoomIsLocked_ReturnsRoomLockedError()
    {
        // Arrange - Create room, register host, and lock room
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, true);
        await Task.Delay(100);

        // New player tries to join
        var playerConnection = CreateHubConnection();
        ErrorDto? receivedError = null;
        playerConnection.On<ErrorDto>("Error", error => receivedError = error);

        await playerConnection.StartAsync();

        // Act
        await playerConnection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), "NewPlayer");

        // Assert
        await Task.Delay(100);
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.RoomLocked);

        await playerConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinRoom_WhenRoomIsLockedButPlayerIsReconnecting_AllowsReconnect()
    {
        // Arrange - Create room, register host, add player, then lock room
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        RoomStateDto? lastHostState = null;
        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => lastHostState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        // Player joins
        var playerId = Guid.NewGuid();
        var player1Connection = CreateHubConnection();
        await player1Connection.StartAsync();
        await player1Connection.InvokeAsync("JoinRoom", roomCode, playerId, "ReconnectingPlayer");
        await Task.Delay(100);

        // Lock the room
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, true);
        await Task.Delay(100);
        lastHostState!.IsLocked.Should().BeTrue();

        // Player disconnects
        await player1Connection.DisposeAsync();
        await Task.Delay(100);
        lastHostState!.Players[0].IsConnected.Should().BeFalse();

        // Act - Player reconnects with same playerId
        var player2Connection = CreateHubConnection();
        RoomStateDto? playerState = null;
        player2Connection.On<RoomStateDto>("LobbyUpdated", state => playerState = state);

        await player2Connection.StartAsync();
        await player2Connection.InvokeAsync("JoinRoom", roomCode, playerId, "ReconnectingPlayer");

        // Assert - Should succeed despite room being locked
        await Task.Delay(100);
        playerState.Should().NotBeNull();
        playerState!.Players.Should().HaveCount(1);
        playerState.Players[0].IsConnected.Should().BeTrue();

        await player2Connection.DisposeAsync();
    }

    [Fact]
    public async Task SetRoomLocked_BroadcastsToAllPlayersInRoom()
    {
        // Arrange - Create room, register host, add player
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        _hostConnection = CreateHubConnection();
        var playerConnection = CreateHubConnection();

        RoomStateDto? hostState = null;
        RoomStateDto? playerState = null;

        _hostConnection.On<RoomStateDto>("LobbyUpdated", state => hostState = state);
        playerConnection.On<RoomStateDto>("LobbyUpdated", state => playerState = state);

        await _hostConnection.StartAsync();
        await _hostConnection.InvokeAsync("RegisterHost", roomCode);

        await playerConnection.StartAsync();
        await playerConnection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), "TestPlayer");
        await Task.Delay(100);

        // Act
        await _hostConnection.InvokeAsync("SetRoomLocked", roomCode, true);

        // Assert
        await Task.Delay(100);
        hostState!.IsLocked.Should().BeTrue();
        playerState!.IsLocked.Should().BeTrue();

        await playerConnection.DisposeAsync();
    }

    #endregion
}
