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
        receivedError!.Code.Should().Be("ROOM_NOT_FOUND");
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
        receivedError!.Code.Should().Be("ROOM_NOT_FOUND");

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
        receivedError!.Code.Should().Be("NAME_INVALID");

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
        receivedError!.Code.Should().Be("NAME_TAKEN");

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
}
